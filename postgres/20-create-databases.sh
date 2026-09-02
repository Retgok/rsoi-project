#!/usr/bin/env bash
set -e

export VARIANT="v1"
export SCRIPT_PATH=/docker-entrypoint-initdb.d/
export PGPASSWORD=postgres
export POSTGRES_USER=postgres
psql -f "$SCRIPT_PATH/scripts/db-$VARIANT.sql"

grant_schema() {
  local db="$1"
  psql --username "$POSTGRES_USER" -d "$db" <<-EOSQL
		GRANT ALL PRIVILEGES ON ALL TABLES IN SCHEMA public TO program;
		GRANT ALL PRIVILEGES ON ALL SEQUENCES IN SCHEMA public TO program;
		ALTER DEFAULT PRIVILEGES IN SCHEMA public GRANT ALL ON TABLES TO program;
		ALTER DEFAULT PRIVILEGES IN SCHEMA public GRANT ALL ON SEQUENCES TO program;
EOSQL
}

psql --username "$POSTGRES_USER" -d tickets     -f "$SCRIPT_PATH/scripts/tables-tickets.sql"
grant_schema tickets

psql --username "$POSTGRES_USER" -d flights     -f "$SCRIPT_PATH/scripts/tables-flights.sql"
grant_schema flights

psql --username "$POSTGRES_USER" -d privileges  -f "$SCRIPT_PATH/scripts/tables-privileges.sql"
grant_schema privileges

psql --username "$POSTGRES_USER" -d identity    -f "$SCRIPT_PATH/scripts/tables-identity.sql"
grant_schema identity

psql --username "$POSTGRES_USER" -d statistics  -f "$SCRIPT_PATH/scripts/tables-statistics.sql"
grant_schema statistics
