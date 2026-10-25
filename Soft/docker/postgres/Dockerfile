FROM postgres:16.15-alpine@sha256:f4610795587492cd4fb887c2dc3de301b813278bb6f616992061141b5f15ac36

COPY init-scripts/ /docker-entrypoint-initdb.d/

ENV POSTGRES_USER=postgres
ENV POSTGRES_PASSWORD=postgres
ENV PGDATA=/var/lib/postgresql/data/pgdata

EXPOSE 5432

HEALTHCHECK --interval=5s --timeout=3s --retries=10 \
    CMD pg_isready -U postgres || exit 1
