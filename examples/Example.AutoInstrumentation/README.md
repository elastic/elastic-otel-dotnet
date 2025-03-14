# Example auto instrumentation plugin for OpenTelemetry .NET

This is a very minimal .NET application that we use to validate our OpenTelemetry plugin loads correctly

This happens automatically through our testing setup:

```bash
$ ./build.sh test --test-suite=integration
```

Which ends up running the tests in `/tests/AutoInstrumentation.IntegrationTests`

To quickly see the `DockerFile` in action run the following from the root of this repository.

```bash
$ docker build -t example.autoinstrumentation:latest -f examples/Example.AutoInstrumentation/Dockerfile --no-cache . && \
    docker run -it --rm -p 5000:8080 --name autoin example.autoinstrumentation:latest
```

```bash
docker build -t distribution.autoinstrumentation:latest -f examples/Example.AutoInstrumentation/distribution.Dockerfile --platform linux/arm64 --no-cache . && \
    docker run -it --rm -p 5000:8080 --name distri --platform linux/arm64 distribution.autoinstrumentation:latest
```