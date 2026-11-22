FROM python:3.13-alpine
WORKDIR /fixture
COPY fake_ai.py ./
USER 65534:65534
CMD ["python", "-u", "fake_ai.py"]
