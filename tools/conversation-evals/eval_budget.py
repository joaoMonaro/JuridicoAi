"""Cost accounting for a single, explicitly bounded evaluation run.
No credential or provider reasoning is included in persisted events.
"""
from decimal import Decimal, InvalidOperation
import hashlib
import json
import threading

class BudgetExceeded(RuntimeError):
    pass

class InvalidUsage(RuntimeError):
    pass

def decimal_value(value):
    try:
        result = Decimal(str(value))
    except (InvalidOperation, ValueError):
        raise ValueError('Invalid decimal amount') from None
    if not result.is_finite() or result < 0:
        raise ValueError('Amount must be finite and nonnegative')
    return result

def fingerprint(payload):
    normalized = {key: value for key, value in payload.items()
                  if key not in {'include', 'stream', 'store', 'max_output_tokens', 'truncation'}}
    return hashlib.sha256(json.dumps(normalized, sort_keys=True, ensure_ascii=False,
                                     separators=(',', ':')).encode()).hexdigest()

def token_count(value):
    if not isinstance(value, int) or isinstance(value, bool) or value < 0:
        raise InvalidUsage('Missing or invalid usage; pessimistic reservation retained')
    return value

class RunBudget:
    def __init__(self, maximum_usd, maximum_calls, input_price, cached_price, output_price, persist=None):
        self.maximum = decimal_value(maximum_usd)
        self.input_price = decimal_value(input_price)
        self.cached_price = decimal_value(cached_price)
        self.output_price = decimal_value(output_price)
        if self.maximum <= 0 or self.input_price <= 0 or self.output_price <= 0:
            raise ValueError('Explicit positive budget and verified prices are required')
        if self.cached_price > self.input_price or not isinstance(maximum_calls, int) or maximum_calls <= 0:
            raise ValueError('Invalid cached price or call limit')
        self.maximum_calls = maximum_calls
        self.events = []
        self.lock = threading.Lock()
        self.persist = persist or (lambda events: None)

    def snapshot(self):
        with self.lock:
            return self._snapshot()

    def _snapshot(self):
        return {'maximumUsd': str(self.maximum), 'maximumCalls': self.maximum_calls,
                'calls': len(self.events), 'debitedOrReservedUsd': str(sum(
                    (Decimal(event['chargedUsd']) for event in self.events), Decimal(0))),
                'unknownUsageCalls': sum(event['state'] != 'settled' for event in self.events),
                'events': [dict(event) for event in self.events]}

    def reserve(self, input_tokens, maximum_output_tokens, request_hash):
        counted = token_count(input_tokens)
        output = token_count(maximum_output_tokens)
        if output == 0:
            raise ValueError('Output reservation must be positive')
        reserved = (counted * self.input_price + output * self.output_price) / Decimal(1_000_000)
        with self.lock:
            used = sum((Decimal(event['chargedUsd']) for event in self.events), Decimal(0))
            if len(self.events) >= self.maximum_calls or used + reserved > self.maximum:
                raise BudgetExceeded('Explicit evaluation budget would be exceeded')
            event = {'ordinal': len(self.events) + 1, 'requestSha256': request_hash,
                     'state': 'reserved', 'reservedInputTokens': counted,
                     'reservedOutputTokens': output, 'chargedUsd': str(reserved)}
            self.events.append(event)
            try:
                self.persist(self._snapshot())
            except Exception:
                self.events.pop()
                raise
            return event['ordinal']

    def settle(self, ordinal, usage, model):
        inputs = token_count(usage.get('input_tokens'))
        outputs = token_count(usage.get('output_tokens'))
        details = usage.get('input_tokens_details') or {}
        cached = token_count(details.get('cached_tokens', 0))
        if cached > inputs:
            raise InvalidUsage('Cached input cannot exceed total input')
        with self.lock:
            event = self.events[ordinal - 1]
            if event['state'] == 'settled':
                raise InvalidUsage('Call already settled')
            if inputs > event['reservedInputTokens'] or outputs > event['reservedOutputTokens']:
                raise InvalidUsage('Usage exceeded its reservation; stop the run and audit pricing/counts')
            cost = ((inputs - cached) * self.input_price + cached * self.cached_price +
                    outputs * self.output_price) / Decimal(1_000_000)
            previous = dict(event)
            event.update(state='settled', inputTokens=inputs, cachedInputTokens=cached,
                         outputTokens=outputs, model=model, chargedUsd=str(cost))
            try:
                self.persist(self._snapshot())
            except Exception:
                event.clear()
                event.update(previous)
                raise
