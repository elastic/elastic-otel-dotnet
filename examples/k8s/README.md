# Run Elastic Distribution of OpenTelemetry .NET on k8s

The following documents how to auto instrument using the OpenTelemetry k8s Operator.

First create a namespace for your k8s deployment:

```bash
kubectl create namespace my-dotnet-ns
```

Next up we'll set our Elastic Cloud endpoint and key as k8s secrets.

```bash
kubectl create secret generic elastic-otel -n my-dotnet-ns \
  "--from-literal=endpoint=<cloud_endpoint>" \
  "--from-literal=apiKey=Authorization=Bearer <api_key>"
```

Next create an `Instrumentation` resource by creating an [`elastic-otel-dotnet.yml`](elastic-otel-dotnet.yml) file.

The manifest sets `OTEL_DOTNET_AUTO_PLUGINS` in `spec.dotnet.env` to load the EDOT .NET plugin.
Keep this setting when customizing the manifest: the operator copies the instrumentation files
but does not run `instrument.sh` or propagate the instrumentation image's environment variables
to the application container. Selecting the EDOT image alone does not load the plugin.

Then apply it to create it in our namespace

```bash
kubectl apply -f elastic-otel-dotnet.yml -n my-dotnet-ns
```

We then edit the namespace to make sure our Instrumentation annotations get applied always:

```bash
kubectl edit namespace my-dotnet-ns
```
ensure the following `instrumentation` gets added under `metadata>annotations`

```yml
apiVersion: v1
kind: Namespace
metadata:
  annotations:
    instrumentation.opentelemetry.io/inject-dotnet: elastic-otel-dotnet
```

We can now create our pod containing our dotnet image.

To add your containerized image create a new `my-dotnet-application.yml` file

```yml
apiVersion: v1
kind: Pod
metadata:
  name: my-dotnet-application
  namespace: my-dotnet-ns
  labels:
    app: my-dotnet-application
spec:
  containers:
    - image: _YOUR_APPLICATIONS_DOCKER_URL_
      imagePullPolicy: Always
      name: my-dotnet-application
```

We can then spin up this pod by applying the template

```bash
kubectl apply -f my-dotnet-application.yml -n my-dotnet-ns
```

Once spun up we can query the logs with 

```bash
kubectl logs my-dotnet-application -n my-dotnet-ns
```

The application logs should include the EDOT .NET startup message (the version varies):

```log
Elastic Distribution of OpenTelemetry (EDOT) .NET: <version>
```

This message confirms that the EDOT plugin loaded, but does not by itself confirm that the native profiler attached.

TIP: You can expose this pod locally to your host using:

```bash
kubectl port-forward -n my-dotnet-ns pods/my-dotnet-application 8081:8080
```

Here we forward the container port `8080` to your local port `8081` allowing you to browse your application.




### Use a local image as pod image

Useful when developing.

```bash
docker build . -t asp-net-example -f examples/Example.AspNetCore.Mvc/Dockerfile
minikube image load asp-net-example:latest --daemon
```

This ensures minikube can resolve `asp-net-example:latest`, you can now update your 
application spec section to:

```yml
spec:
  containers:
    - image: asp-net-example:latest
      imagePullPolicy: Never
      name: asp-net-example
```

NOTE: Make sure `imagePullPolicy` is set to `Never`


### Debug deployments 

The `describe` command shows the init-container status and the environment variables
injected into the application container.

```bash
kubectl describe pod my-dotnet-application -n my-dotnet-ns
```

You can use `exec` to inspect the container to see if it matches your expectations.
```log
kubectl exec my-dotnet-application -n my-dotnet-ns -- ls -la /otel-auto-instrumentation-dotnet
```

### Run operator image smoke tests

The smoke tests exercise the root `Dockerfile` without requiring a Kubernetes cluster.
They copy its payload as the nonroot init-container user, then run an uninstrumented .NET
application using the operator's environment variables and a read-only instrumentation volume.
They cover Linux amd64 and arm64, glibc and musl, and both legacy and current profiler paths.
Assertions include native-profiler attachment, EDOT plugin initialization, and copied symlinks.

Docker with Buildx, the .NET SDK, and arm64 execution support (a native arm64 host or QEMU
on amd64) are required. Hosts must also support amd64 execution. Docker Desktop typically
provides cross-architecture execution; Linux CI installs QEMU explicitly.

```bash
dotnet format
./build.sh redistribute
export EDOT_OPERATOR_SMOKE_IMAGE_PREFIX=edot-operator-smoke
docker buildx build --platform linux/amd64 --load --tag "$EDOT_OPERATOR_SMOKE_IMAGE_PREFIX:amd64" .
docker buildx build --platform linux/arm64 --load --tag "$EDOT_OPERATOR_SMOKE_IMAGE_PREFIX:arm64" .
dotnet test tests/Elastic.OpenTelemetry.IntegrationTests/Elastic.OpenTelemetry.IntegrationTests.csproj \
  -c Release --filter FullyQualifiedName~OperatorImageSmokeTests -bl:{}
docker image rm "$EDOT_OPERATOR_SMOKE_IMAGE_PREFIX:amd64" "$EDOT_OPERATOR_SMOKE_IMAGE_PREFIX:arm64"
```

The tests are skipped when `EDOT_OPERATOR_SMOKE_IMAGE_PREFIX` is unset. Linux integration
CI builds both images and sets this variable, so all eight combinations run there.
