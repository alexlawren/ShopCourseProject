-- Create logical databases for ShopCourseProject microservices
-- Executed on initial volume creation in /docker-entrypoint-initdb.d/

SELECT 'CREATE DATABASE shop_identity' WHERE NOT EXISTS (SELECT FROM pg_database WHERE datname = 'shop_identity')\gexec
SELECT 'CREATE DATABASE shop_catalog' WHERE NOT EXISTS (SELECT FROM pg_database WHERE datname = 'shop_catalog')\gexec
SELECT 'CREATE DATABASE shop_orders' WHERE NOT EXISTS (SELECT FROM pg_database WHERE datname = 'shop_orders')\gexec
