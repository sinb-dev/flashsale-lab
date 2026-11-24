FROM mcr.microsoft.com/mssql/server:2022-latest

ENV ACCEPT_EULA=Y \
    MSSQL_PID=Developer

USER root
COPY entrypoint.sh /usr/src/app/entrypoint.sh
COPY init/ /usr/src/app/init/
RUN chmod +x /usr/src/app/entrypoint.sh
USER mssql

ENTRYPOINT ["/usr/src/app/entrypoint.sh"]
