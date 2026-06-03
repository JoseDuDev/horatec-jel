-- Script de inicialização do PostgreSQL para o Horafy
-- Executado apenas na primeira criação do container

-- Extensões necessárias
CREATE EXTENSION IF NOT EXISTS "uuid-ossp";
CREATE EXTENSION IF NOT EXISTS "pg_trgm";      -- Busca por trigrama (pesquisa de texto)
CREATE EXTENSION IF NOT EXISTS "unaccent";       -- Busca sem acento

-- Schema público (tabelas compartilhadas entre todos os tenants)
-- O schema 'public' já existe por padrão no PostgreSQL

-- Função auxiliar para criar schema de tenant
CREATE OR REPLACE FUNCTION create_tenant_schema(p_schema_name TEXT)
RETURNS VOID AS $$
BEGIN
    EXECUTE format('CREATE SCHEMA IF NOT EXISTS %I', p_schema_name);
END;
$$ LANGUAGE plpgsql;

COMMENT ON FUNCTION create_tenant_schema IS
    'Cria o schema isolado de um tenant. Chamada pelo TenantMigrationService.';
