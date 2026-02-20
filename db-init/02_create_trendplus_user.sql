-- Create role and grant privileges for local development
DO
$do$
BEGIN
   IF NOT EXISTS (SELECT 1 FROM pg_roles WHERE rolname = 'trendplus') THEN
      CREATE ROLE trendplus LOGIN PASSWORD 'trendplus';
   END IF;
END
$do$;

GRANT ALL PRIVILEGES ON DATABASE trendplus TO trendplus;
GRANT ALL PRIVILEGES ON DATABASE analytics TO trendplus;
