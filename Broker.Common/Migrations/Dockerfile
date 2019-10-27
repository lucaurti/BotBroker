FROM microsoft/dotnet:2.2-runtime-deps

VOLUME ["/app/Exposed"]
COPY publish/ app/
RUN chmod 755 /app/Broker.Batch
RUN mv /app/wwwroot /

ENTRYPOINT ["./app/Broker.Batch"]