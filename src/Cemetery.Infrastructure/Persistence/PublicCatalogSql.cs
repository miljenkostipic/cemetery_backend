namespace Cemetery.Infrastructure.Persistence;

internal static class PublicCatalogSql
{
    public const string Up = """
        CREATE EXTENSION IF NOT EXISTS unaccent;

        CREATE OR REPLACE FUNCTION cemetery.fold_text(value text)
        RETURNS text
        LANGUAGE sql
        STABLE
        AS $$
          SELECT replace(lower(public.unaccent(coalesce(value, ''))), 'đ', 'd')
        $$;

        CREATE OR REPLACE FUNCTION cemetery.publicly_listed(p_today date)
        RETURNS TABLE (
          deceased_id uuid,
          given_name text,
          family_name text,
          born_on date,
          died_on date,
          epitaph text,
          photo_url text,
          organization_slug text,
          organization_name text,
          cemetery_id uuid,
          cemetery_name text,
          schematic boolean,
          grave_site_id uuid,
          grave_site_code text,
          outline_json text
        )
        LANGUAGE sql
        STABLE
        SECURITY DEFINER
        SET search_path = cemetery, public
        AS $$
          SELECT
            d.id,
            d.given_name,
            d.family_name,
            d.born_on,
            d.died_on,
            d.epitaph,
            d.photo_url,
            o.slug,
            o.name,
            c.id,
            c.name,
            c.schematic,
            g.id,
            g.code,
            CASE WHEN g.outline IS NULL THEN NULL ELSE ST_AsGeoJSON(g.outline) END
          FROM cemetery.deceased AS d
          JOIN cemetery.organizations AS o ON o.id = d.tenant_id
          JOIN cemetery.cemeteries AS c ON c.tenant_id = d.tenant_id AND c.id = d.cemetery_id
          LEFT JOIN cemetery.interments AS i
            ON i.tenant_id = d.tenant_id
           AND i.deceased_id = d.id
           AND i.exhumed_on IS NULL
           AND i.transferred_on IS NULL
           AND i.superseded_on IS NULL
          LEFT JOIN cemetery.grave_sites AS g
            ON g.tenant_id = i.tenant_id
           AND g.id = i.grave_site_id
          WHERE o.publishes_register
            AND d.removed_at IS NULL
            AND COALESCE(g.hidden_from_public, false) = false
            AND (
              o.hide_recent_deaths_days = 0
              OR (
                d.died_on IS NOT NULL
                AND d.died_on + make_interval(days => o.hide_recent_deaths_days) <= p_today
              )
            )
        $$;

        CREATE OR REPLACE FUNCTION cemetery.public_search(
          p_name text,
          p_year_from integer,
          p_year_to integer,
          p_slug text,
          p_today date)
        RETURNS TABLE (
          deceased_id uuid,
          given_name text,
          family_name text,
          born_on date,
          died_on date,
          organization_slug text,
          organization_name text,
          cemetery_id uuid,
          cemetery_name text,
          grave_site_id uuid,
          grave_site_code text
        )
        LANGUAGE sql
        STABLE
        SECURITY DEFINER
        SET search_path = cemetery, public
        AS $$
          SELECT
            listed.deceased_id,
            listed.given_name,
            listed.family_name,
            listed.born_on,
            listed.died_on,
            listed.organization_slug,
            listed.organization_name,
            listed.cemetery_id,
            listed.cemetery_name,
            listed.grave_site_id,
            listed.grave_site_code
          FROM cemetery.publicly_listed(p_today) AS listed
          WHERE btrim(coalesce(p_name, '')) <> ''
            AND (p_slug IS NULL OR btrim(p_slug) = '' OR listed.organization_slug = p_slug)
            AND (
              p_year_from IS NULL
              OR COALESCE(EXTRACT(YEAR FROM listed.died_on), EXTRACT(YEAR FROM listed.born_on)) >= p_year_from
            )
            AND (
              p_year_to IS NULL
              OR COALESCE(EXTRACT(YEAR FROM listed.born_on), EXTRACT(YEAR FROM listed.died_on)) <= p_year_to
            )
            AND NOT EXISTS (
              SELECT 1
              FROM regexp_split_to_table(cemetery.fold_text(p_name), '[[:space:]]+') AS token
              WHERE token <> ''
                AND cemetery.fold_text(listed.given_name || ' ' || listed.family_name)
                  NOT LIKE '%' || replace(replace(replace(token, '\', '\\'), '%', '\%'), '_', '\_') || '%' ESCAPE '\'
            )
          ORDER BY listed.family_name, listed.given_name
          LIMIT 50
        $$;

        CREATE OR REPLACE FUNCTION cemetery.public_memorial(p_id uuid, p_today date)
        RETURNS TABLE (
          deceased_id uuid,
          given_name text,
          family_name text,
          born_on date,
          died_on date,
          epitaph text,
          photo_url text,
          organization_slug text,
          organization_name text,
          cemetery_id uuid,
          cemetery_name text,
          schematic boolean,
          grave_site_id uuid,
          grave_site_code text,
          outline_json text
        )
        LANGUAGE sql
        STABLE
        SECURITY DEFINER
        SET search_path = cemetery, public
        AS $$
          SELECT
            listed.deceased_id,
            listed.given_name,
            listed.family_name,
            listed.born_on,
            listed.died_on,
            listed.epitaph,
            listed.photo_url,
            listed.organization_slug,
            listed.organization_name,
            listed.cemetery_id,
            listed.cemetery_name,
            listed.schematic,
            listed.grave_site_id,
            listed.grave_site_code,
            listed.outline_json
          FROM cemetery.publicly_listed(p_today) AS listed
          WHERE listed.deceased_id = p_id
        $$;

        CREATE OR REPLACE FUNCTION cemetery.public_cemeteries()
        RETURNS TABLE (
          organization_slug text,
          organization_name text,
          cemetery_id uuid,
          cemetery_name text,
          schematic boolean
        )
        LANGUAGE sql
        STABLE
        SECURITY DEFINER
        SET search_path = cemetery, public
        AS $$
          SELECT o.slug, o.name, c.id, c.name, c.schematic
          FROM cemetery.organizations AS o
          JOIN cemetery.cemeteries AS c ON c.tenant_id = o.id
          WHERE o.publishes_register
          ORDER BY o.name, c.name
        $$;

        CREATE OR REPLACE FUNCTION cemetery.public_place(p_slug text, p_cemetery_id uuid)
        RETURNS TABLE (
          organization_slug text,
          organization_name text,
          cemetery_id uuid,
          cemetery_name text,
          schematic boolean,
          plan_image_url text,
          plan_json text
        )
        LANGUAGE sql
        STABLE
        SECURITY DEFINER
        SET search_path = cemetery, public
        AS $$
          SELECT
            o.slug,
            o.name,
            c.id,
            c.name,
            c.schematic,
            c.plan_image_url,
            CASE WHEN c.plan_bounds IS NULL THEN NULL ELSE ST_AsGeoJSON(c.plan_bounds) END
          FROM cemetery.organizations AS o
          JOIN cemetery.cemeteries AS c ON c.tenant_id = o.id
          WHERE o.publishes_register
            AND o.slug = p_slug
            AND c.id = p_cemetery_id
        $$;

        CREATE OR REPLACE FUNCTION cemetery.public_sections(p_slug text, p_cemetery_id uuid)
        RETURNS TABLE (
          id uuid,
          name text,
          code text,
          outline_json text
        )
        LANGUAGE sql
        STABLE
        SECURITY DEFINER
        SET search_path = cemetery, public
        AS $$
          SELECT s.id, s.name, s.code, CASE WHEN s.outline IS NULL THEN NULL ELSE ST_AsGeoJSON(s.outline) END
          FROM cemetery.sections AS s
          JOIN cemetery.organizations AS o ON o.id = s.tenant_id
          WHERE o.publishes_register
            AND o.slug = p_slug
            AND s.cemetery_id = p_cemetery_id
          ORDER BY s.code
        $$;

        CREATE OR REPLACE FUNCTION cemetery.public_sites(p_slug text, p_cemetery_id uuid)
        RETURNS TABLE (
          id uuid,
          code text,
          outline_json text
        )
        LANGUAGE sql
        STABLE
        SECURITY DEFINER
        SET search_path = cemetery, public
        AS $$
          SELECT g.id, g.code, CASE WHEN g.outline IS NULL THEN NULL ELSE ST_AsGeoJSON(g.outline) END
          FROM cemetery.grave_sites AS g
          JOIN cemetery.organizations AS o ON o.id = g.tenant_id
          WHERE o.publishes_register
            AND o.slug = p_slug
            AND g.cemetery_id = p_cemetery_id
            AND NOT g.hidden_from_public
          ORDER BY g.code
        $$;

        REVOKE ALL ON FUNCTION cemetery.fold_text(text) FROM PUBLIC;
        REVOKE ALL ON FUNCTION cemetery.publicly_listed(date) FROM PUBLIC;
        REVOKE ALL ON FUNCTION cemetery.public_search(text, integer, integer, text, date) FROM PUBLIC;
        REVOKE ALL ON FUNCTION cemetery.public_memorial(uuid, date) FROM PUBLIC;
        REVOKE ALL ON FUNCTION cemetery.public_cemeteries() FROM PUBLIC;
        REVOKE ALL ON FUNCTION cemetery.public_place(text, uuid) FROM PUBLIC;
        REVOKE ALL ON FUNCTION cemetery.public_sections(text, uuid) FROM PUBLIC;
        REVOKE ALL ON FUNCTION cemetery.public_sites(text, uuid) FROM PUBLIC;
        GRANT EXECUTE ON FUNCTION cemetery.fold_text(text) TO cemetery_app;
        GRANT EXECUTE ON FUNCTION cemetery.publicly_listed(date) TO cemetery_app;
        GRANT EXECUTE ON FUNCTION cemetery.public_search(text, integer, integer, text, date) TO cemetery_app;
        GRANT EXECUTE ON FUNCTION cemetery.public_memorial(uuid, date) TO cemetery_app;
        GRANT EXECUTE ON FUNCTION cemetery.public_cemeteries() TO cemetery_app;
        GRANT EXECUTE ON FUNCTION cemetery.public_place(text, uuid) TO cemetery_app;
        GRANT EXECUTE ON FUNCTION cemetery.public_sections(text, uuid) TO cemetery_app;
        GRANT EXECUTE ON FUNCTION cemetery.public_sites(text, uuid) TO cemetery_app;
        """;

    public const string Down = """
        DROP FUNCTION IF EXISTS cemetery.public_sites(text, uuid);
        DROP FUNCTION IF EXISTS cemetery.public_sections(text, uuid);
        DROP FUNCTION IF EXISTS cemetery.public_place(text, uuid);
        DROP FUNCTION IF EXISTS cemetery.public_cemeteries();
        DROP FUNCTION IF EXISTS cemetery.public_memorial(uuid, date);
        DROP FUNCTION IF EXISTS cemetery.public_search(text, integer, integer, text, date);
        DROP FUNCTION IF EXISTS cemetery.publicly_listed(date);
        DROP FUNCTION IF EXISTS cemetery.fold_text(text);
        """;
}
