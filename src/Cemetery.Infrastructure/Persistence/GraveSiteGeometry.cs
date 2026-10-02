namespace Cemetery.Infrastructure.Persistence;

public static class GraveSiteGeometry
{
    public const string Up = """
        CREATE OR REPLACE FUNCTION cemetery.grave_sites_reject_overlap()
        RETURNS trigger
        LANGUAGE plpgsql
        SECURITY DEFINER
        SET search_path = cemetery, public
        AS $fn$
        BEGIN
          IF NEW.outline IS NULL OR NEW.closed THEN
            RETURN NEW;
          END IF;
          IF EXISTS (
            SELECT 1
            FROM cemetery.grave_sites AS other
            WHERE other.tenant_id = NEW.tenant_id
              AND other.section_id = NEW.section_id
              AND other.id <> NEW.id
              AND other.closed = false
              AND other.outline IS NOT NULL
              AND ST_Area(ST_Intersection(other.outline, NEW.outline)) > 0.000000000001
          ) THEN
            RAISE EXCEPTION 'grave_site.overlaps' USING ERRCODE = '23514';
          END IF;
          RETURN NEW;
        END;
        $fn$;

        DROP TRIGGER IF EXISTS grave_sites_no_overlap ON cemetery.grave_sites;
        CREATE CONSTRAINT TRIGGER grave_sites_no_overlap
          AFTER INSERT OR UPDATE OF outline, section_id, tenant_id, closed
          ON cemetery.grave_sites
          DEFERRABLE INITIALLY DEFERRED
          FOR EACH ROW
          EXECUTE FUNCTION cemetery.grave_sites_reject_overlap();
        """;
}
