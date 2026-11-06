namespace Cemetery.Infrastructure.Persistence;

internal static class Phase4Sql
{
    public const string Up = """
        UPDATE cemetery.grave_sites AS site
        SET
          used = COALESCE(sub.used, 0),
          rest_until = sub.rest_until,
          status = CASE
            WHEN site.closed THEN 'closed'
            WHEN site.outline IS NULL THEN 'planned'
            WHEN COALESCE(sub.used, 0) <= 0 AND COALESCE(sub.rested, 0) > 0 THEN 'reusable'
            WHEN COALESCE(sub.used, 0) <= 0 THEN 'available'
            WHEN site.capacity > 0 AND COALESCE(sub.used, 0) >= site.capacity THEN 'full'
            ELSE 'occupied'
          END
        FROM (
          SELECT
            row.id,
            COUNT(*) FILTER (
              WHERE burial.exhumed_on IS NULL AND burial.transferred_on IS NULL AND burial.superseded_on IS NULL
                AND burial.buried_on + make_interval(years => place.rest_period_years) > CURRENT_DATE
            )::int AS used,
            MIN(burial.buried_on + make_interval(years => place.rest_period_years)) FILTER (
              WHERE burial.exhumed_on IS NULL AND burial.transferred_on IS NULL AND burial.superseded_on IS NULL
            ) AS rest_until,
            COUNT(*) FILTER (
              WHERE burial.exhumed_on IS NULL AND burial.transferred_on IS NULL AND burial.superseded_on IS NULL
                AND burial.buried_on + make_interval(years => place.rest_period_years) <= CURRENT_DATE
            )::int AS rested
          FROM cemetery.grave_sites AS row
          JOIN cemetery.cemeteries AS place ON place.id = row.cemetery_id
          LEFT JOIN cemetery.interments AS burial ON burial.grave_site_id = row.id
          GROUP BY row.id
        ) AS sub
        WHERE site.id = sub.id;

        CREATE EXTENSION IF NOT EXISTS pg_trgm;

        CREATE OR REPLACE FUNCTION cemetery.fold_text_immutable(value text)
        RETURNS text
        LANGUAGE plpgsql
        IMMUTABLE
        AS $$
        BEGIN
          RETURN replace(lower(public.unaccent(coalesce(value, ''))), 'đ', 'd');
        END;
        $$;

        CREATE INDEX IF NOT EXISTS deceased_name_trgm
          ON cemetery.deceased USING gin (cemetery.fold_text_immutable(given_name || ' ' || family_name) gin_trgm_ops);
        CREATE INDEX IF NOT EXISTS grave_sites_code_trgm
          ON cemetery.grave_sites USING gin (cemetery.fold_text_immutable(code) gin_trgm_ops);
        CREATE INDEX IF NOT EXISTS holders_name_trgm
          ON cemetery.holders USING gin (cemetery.fold_text_immutable(given_name || ' ' || family_name) gin_trgm_ops);

        DROP FUNCTION IF EXISTS cemetery.public_memorial(uuid, date);

        CREATE FUNCTION cemetery.public_memorial(p_id uuid, p_today date)
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
          outline_json text,
          centroid_longitude double precision,
          centroid_latitude double precision,
          section_name text,
          entrance_name text,
          entrance_longitude double precision,
          entrance_latitude double precision,
          others_json text
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
            listed.outline_json,
            ST_X(ST_Centroid(site.outline)),
            ST_Y(ST_Centroid(site.outline)),
            section.name,
            entrance.name,
            ST_X(entrance.location),
            ST_Y(entrance.location),
            (
              SELECT jsonb_agg(jsonb_build_object(
                'deceasedId', other.deceased_id,
                'givenName', other.given_name,
                'familyName', other.family_name
              ))::text
              FROM cemetery.publicly_listed(p_today) AS other
              WHERE other.grave_site_id IS NOT NULL
                AND other.grave_site_id = listed.grave_site_id
                AND other.deceased_id <> listed.deceased_id
            )
          FROM cemetery.publicly_listed(p_today) AS listed
          LEFT JOIN cemetery.grave_sites AS site ON site.id = listed.grave_site_id
          LEFT JOIN cemetery.sections AS section ON section.id = site.section_id
          LEFT JOIN LATERAL (
            SELECT e.name, e.location
            FROM cemetery.entrances AS e
            WHERE site.outline IS NOT NULL AND e.cemetery_id = site.cemetery_id
            ORDER BY e.location <-> ST_Centroid(site.outline)
            LIMIT 1
          ) AS entrance ON true
          WHERE listed.deceased_id = p_id
        $$;

        REVOKE ALL ON FUNCTION cemetery.public_memorial(uuid, date) FROM PUBLIC;
        REVOKE ALL ON FUNCTION cemetery.fold_text_immutable(text) FROM PUBLIC;
        GRANT EXECUTE ON FUNCTION cemetery.public_memorial(uuid, date) TO cemetery_app;
        GRANT EXECUTE ON FUNCTION cemetery.fold_text_immutable(text) TO cemetery_app;
        """;

    public const string Down = """
        DROP INDEX IF EXISTS cemetery.deceased_name_trgm;
        DROP INDEX IF EXISTS cemetery.grave_sites_code_trgm;
        DROP INDEX IF EXISTS cemetery.holders_name_trgm;
        DROP FUNCTION IF EXISTS cemetery.fold_text_immutable(text);
        DROP FUNCTION IF EXISTS cemetery.public_memorial(uuid, date);
        """;
}
