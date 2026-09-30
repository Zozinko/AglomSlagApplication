FROM postgres:16-alpine

COPY init-scripts/ /docker-entrypoint-initdb.d/

ENV POSTGRES_USER=postgres
ENV POSTGRES_PASSWORD=postgres
ENV PGDATA=/var/lib/postgresql/data/pgdata

EXPOSE 5432

HEALTHCHECK --interval=5s --timeout=3s --retries=10 \
    CMD pg_isready -U postgres || exit 1