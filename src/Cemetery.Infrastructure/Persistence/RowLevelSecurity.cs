namespace Cemetery.Infrastructure.Persistence;

public static class RowLevelSecurity
{
    public const string Up = """
        ALTER TABLE cemetery.memberships ENABLE ROW LEVEL SECURITY;
        ALTER TABLE cemetery.memberships FORCE ROW LEVEL SECURITY;
        DROP POLICY IF EXISTS memberships_tenant ON cemetery.memberships;
        CREATE POLICY memberships_tenant ON cemetery.memberships
          USING (
            tenant_id = NULLIF(current_setting('app.tenant_id', true), '')::uuid
            OR user_id = NULLIF(current_setting('app.user_id', true), '')::uuid
          )
          WITH CHECK (
            tenant_id = NULLIF(current_setting('app.tenant_id', true), '')::uuid
          );

        ALTER TABLE cemetery.invitations ENABLE ROW LEVEL SECURITY;
        ALTER TABLE cemetery.invitations FORCE ROW LEVEL SECURITY;
        DROP POLICY IF EXISTS invitations_tenant ON cemetery.invitations;
        CREATE POLICY invitations_tenant ON cemetery.invitations
          USING (
            tenant_id = NULLIF(current_setting('app.tenant_id', true), '')::uuid
            OR (
              NULLIF(current_setting('app.invitation_hash', true), '') IS NOT NULL
              AND token_hash = current_setting('app.invitation_hash', true)
            )
          )
          WITH CHECK (
            tenant_id = NULLIF(current_setting('app.tenant_id', true), '')::uuid
          );

        ALTER TABLE cemetery.organizations ENABLE ROW LEVEL SECURITY;
        ALTER TABLE cemetery.organizations FORCE ROW LEVEL SECURITY;
        DROP POLICY IF EXISTS organizations_tenant ON cemetery.organizations;
        CREATE POLICY organizations_tenant ON cemetery.organizations
          USING (
            id = NULLIF(current_setting('app.tenant_id', true), '')::uuid
            OR EXISTS (
              SELECT 1 FROM cemetery.memberships AS membership
              WHERE membership.tenant_id = organizations.id
                AND membership.user_id = NULLIF(current_setting('app.user_id', true), '')::uuid
            )
          )
          WITH CHECK (
            id = NULLIF(current_setting('app.tenant_id', true), '')::uuid
          );

        DO $body$
        BEGIN
          IF EXISTS (SELECT FROM pg_roles WHERE rolname = 'cemetery_app') THEN
            GRANT USAGE ON SCHEMA cemetery TO cemetery_app;
            GRANT SELECT, INSERT, UPDATE, DELETE ON ALL TABLES IN SCHEMA cemetery TO cemetery_app;
            GRANT USAGE, SELECT ON ALL SEQUENCES IN SCHEMA cemetery TO cemetery_app;
          END IF;
        END
        $body$;
        """;

    public const string Layout = """
        ALTER TABLE cemetery.cemeteries ENABLE ROW LEVEL SECURITY;
        ALTER TABLE cemetery.cemeteries FORCE ROW LEVEL SECURITY;
        DROP POLICY IF EXISTS cemeteries_tenant ON cemetery.cemeteries;
        CREATE POLICY cemeteries_tenant ON cemetery.cemeteries
          USING (tenant_id = NULLIF(current_setting('app.tenant_id', true), '')::uuid)
          WITH CHECK (tenant_id = NULLIF(current_setting('app.tenant_id', true), '')::uuid);

        ALTER TABLE cemetery.sections ENABLE ROW LEVEL SECURITY;
        ALTER TABLE cemetery.sections FORCE ROW LEVEL SECURITY;
        DROP POLICY IF EXISTS sections_tenant ON cemetery.sections;
        CREATE POLICY sections_tenant ON cemetery.sections
          USING (tenant_id = NULLIF(current_setting('app.tenant_id', true), '')::uuid)
          WITH CHECK (tenant_id = NULLIF(current_setting('app.tenant_id', true), '')::uuid);

        ALTER TABLE cemetery.grave_rows ENABLE ROW LEVEL SECURITY;
        ALTER TABLE cemetery.grave_rows FORCE ROW LEVEL SECURITY;
        DROP POLICY IF EXISTS grave_rows_tenant ON cemetery.grave_rows;
        CREATE POLICY grave_rows_tenant ON cemetery.grave_rows
          USING (tenant_id = NULLIF(current_setting('app.tenant_id', true), '')::uuid)
          WITH CHECK (tenant_id = NULLIF(current_setting('app.tenant_id', true), '')::uuid);

        ALTER TABLE cemetery.grave_sites ENABLE ROW LEVEL SECURITY;
        ALTER TABLE cemetery.grave_sites FORCE ROW LEVEL SECURITY;
        DROP POLICY IF EXISTS grave_sites_tenant ON cemetery.grave_sites;
        CREATE POLICY grave_sites_tenant ON cemetery.grave_sites
          USING (tenant_id = NULLIF(current_setting('app.tenant_id', true), '')::uuid)
          WITH CHECK (tenant_id = NULLIF(current_setting('app.tenant_id', true), '')::uuid);

        DO $body$
        BEGIN
          IF EXISTS (SELECT FROM pg_roles WHERE rolname = 'cemetery_app') THEN
            GRANT SELECT, INSERT, UPDATE, DELETE ON ALL TABLES IN SCHEMA cemetery TO cemetery_app;
            GRANT USAGE, SELECT ON ALL SEQUENCES IN SCHEMA cemetery TO cemetery_app;
          END IF;
        END
        $body$;
        """;
}
