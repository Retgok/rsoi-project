CREATE TABLE event_log
(
    id           SERIAL PRIMARY KEY,
    service_name VARCHAR(80)  NOT NULL,
    action       VARCHAR(80)  NOT NULL,
    username     VARCHAR(80),
    details      TEXT,
    duration_ms  INT,
    created_at   TIMESTAMP    NOT NULL DEFAULT NOW()
);

CREATE INDEX idx_event_log_service ON event_log (service_name);
CREATE INDEX idx_event_log_action ON event_log (action);
CREATE INDEX idx_event_log_created ON event_log (created_at);
