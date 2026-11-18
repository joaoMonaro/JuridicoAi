"""Extract only allowlisted technical timings from the isolated API JSON log."""
import json
import math
from pathlib import Path
import uuid

STAGES = {'chat.accept', 'generation.context', 'generation.prepare', 'generation.publish',
          'generation.processing', 'provider.input_tokens', 'provider.headers', 'provider.generation'}
IDS = {'GenerationId', 'ConversationId', 'ClientRequestId', 'CorrelationId'}
REASONS = {'AI_DOCUMENTS_PROCESSING', 'AI_USER_RATE_LIMITED', 'AI_ORGANIZATION_RATE_LIMITED',
           'AI_USER_CONCURRENCY_LIMITED', 'AI_ORGANIZATION_CONCURRENCY_LIMITED'}
STATES = {'NaFila', 'PreparandoContexto', 'AguardandoDocumentos', 'AguardandoAnalise',
          'Executando', 'Redigindo', 'Verificando', 'Publicando'}

def number(value):
    try:
        return isinstance(value, (int, float)) and not isinstance(value, bool) and math.isfinite(value) and value >= 0
    except OverflowError:
        return False

def extract_latency(path):
    events = []
    path = Path(path)
    for line in path.read_text(encoding='utf-8').splitlines() if path.exists() else []:
        try:
            row = json.loads(line)
        except (ValueError, TypeError):
            continue
        if not isinstance(row, dict) or not isinstance(row.get('State'), dict):
            continue
        state, identifier = row['State'], row.get('EventId')
        event = None
        if identifier == 6401 and isinstance(state.get('Stage'), str) and state['Stage'] in STAGES and number(state.get('DurationMs')):
            event = {'kind': 'stage', 'stage': state['Stage'], 'durationMs': state['DurationMs']}
        elif identifier == 6402 and isinstance(state.get('Reason'), str) and state['Reason'] in REASONS and number(state.get('RetryAfterMs')):
            event = {'kind': 'deferred', 'reason': state['Reason'], 'retryAfterMs': state['RetryAfterMs']}
        elif identifier == 6403 and isinstance(state.get('State'), str) and state['State'] in STATES and number(state.get('SinceCreatedMs')):
            event = {'kind': 'claim', 'state': state['State'], 'sinceCreatedMs': state['SinceCreatedMs']}
        if event is None:
            continue
        scopes = row.get('Scopes')
        for scope in scopes if isinstance(scopes, list) else []:
            if not isinstance(scope, dict):
                continue
            for key in IDS:
                try:
                    event[key] = str(uuid.UUID(str(scope[key])))
                except (KeyError, ValueError, TypeError, AttributeError):
                    pass
        events.append(event)
    return {'schemaVersion': 'conversation-latency-v1', 'available': bool(events), 'events': events,
            'interpretation': 'Nested stage durations overlap; do not sum them as independent costs or time.'}
