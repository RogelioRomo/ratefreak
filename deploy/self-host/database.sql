-- Fresh database initialization only, run by the PostgreSQL container identity.
-- psql reads protected files without putting passwords in process arguments.
-- Password literals must also stay out of failure/duration statement diagnostics.
-- These settings affect this bootstrap connection only, not application logging.
SET log_statement = 'none';
SET log_min_error_statement = 'panic';
SET log_min_duration_statement = -1;
SET log_min_duration_sample = -1;
\set migrator_password `cat /run/ratefreak/migrator`
\set runtime_password `cat /run/ratefreak/runtime`
CREATE ROLE ratefreak_migrator LOGIN PASSWORD :'migrator_password' NOSUPERUSER NOCREATEDB NOCREATEROLE;
CREATE ROLE ratefreak_runtime LOGIN PASSWORD :'runtime_password' NOSUPERUSER NOCREATEDB NOCREATEROLE;
ALTER DATABASE ratefreak OWNER TO ratefreak_migrator;
ALTER SCHEMA public OWNER TO ratefreak_migrator;
REVOKE CREATE ON SCHEMA public FROM PUBLIC;
GRANT USAGE ON SCHEMA public TO ratefreak_runtime;
ALTER DEFAULT PRIVILEGES FOR ROLE ratefreak_migrator IN SCHEMA public GRANT SELECT, INSERT, UPDATE, DELETE ON TABLES TO ratefreak_runtime;
ALTER DEFAULT PRIVILEGES FOR ROLE ratefreak_migrator IN SCHEMA public GRANT USAGE, SELECT ON SEQUENCES TO ratefreak_runtime;
