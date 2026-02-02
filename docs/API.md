# API (Living Contract)

All endpoints are versioned under `/api/v1`.

## System Endpoints

### Health Check
```
GET /health
```
Returns detailed health status with database connectivity check.

**Response (200 OK)**
```json
{
  "status": "Healthy",
  "checks": [
    {
      "name": "database",
      "status": "Healthy",
      "description": null,
      "duration": 1.234
    }
  ],
  "totalDuration": 2.345
}
```

### Ping (Liveness)
```
GET /ping
```
Simple liveness check returning "pong".

**Response (200 OK)**
```json
"pong"
```

### System Status
```
GET /api/v1/system/status
```
Returns application info.

**Response (200 OK)**
```json
{
  "appName": "Shortboxerr",
  "version": "0.1.0",
  "startTime": "2026-02-02T03:27:33.000Z"
}
```

## OpenAPI / Swagger

- **Swagger UI**: `GET /swagger`
- **OpenAPI Spec**: `GET /swagger/v1/swagger.json`

---

(Expand this file as endpoints are added.)
