FROM python:3.12-slim
WORKDIR /app
COPY ml/requirements.txt .
RUN pip install --no-cache-dir -r requirements.txt
COPY ml/ .
EXPOSE 8001
HEALTHCHECK --interval=15s --timeout=5s --start-period=10s CMD python -c "import urllib.request;urllib.request.urlopen('http://localhost:8001/health')" || exit 1
CMD ["uvicorn", "service.main:app", "--host", "0.0.0.0", "--port", "8001"]
