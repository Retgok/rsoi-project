CREATE TABLE users
(
    id            SERIAL PRIMARY KEY,
    username      VARCHAR(80)  NOT NULL UNIQUE,
    password_hash VARCHAR(255) NOT NULL,
    email         VARCHAR(255),
    first_name    VARCHAR(255),
    last_name     VARCHAR(255),
    role          VARCHAR(20)  NOT NULL DEFAULT 'User'
        CHECK (role IN ('User', 'Admin'))
);

CREATE TABLE oauth_clients
(
    client_id     VARCHAR(80) PRIMARY KEY,
    client_secret VARCHAR(255) NOT NULL,
    redirect_uris TEXT         NOT NULL
);

CREATE TABLE auth_codes
(
    code          VARCHAR(255) PRIMARY KEY,
    client_id     VARCHAR(80)  NOT NULL,
    username      VARCHAR(80)  NOT NULL,
    redirect_uri  TEXT         NOT NULL,
    scope         TEXT         NOT NULL,
    expires_at    TIMESTAMP    NOT NULL
);
