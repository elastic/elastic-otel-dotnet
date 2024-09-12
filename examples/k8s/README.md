# Run Elastic Distribution of OpenTelemetry .NET on k8s

The following documents how to auto instrument using the OpenTelemetry k8s Operator.

First create a namespace for your k8s deployment:

```bash
kubectl create namespace my-dotnet-ns
```

Next up we'll set our Elastic Cloud endpoint and key as k8s secrets.

```bash
kubectl create secret generic elastic-otel -my-dotnet-ns \
  "--from-literal=endpoint=<cloud_endpoint>" \
  "--from-literal=apiKey=Authorization=Bearer <api_key>"
```

Next create an `Instrumentation` resource by creating an [`elastic-otel-dotnet.yml`](elastic-otel-dotnet.yml) file.

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
  namespace: my-dotnet-application
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

It should print the Elastic Distribution of OpenTelemetry .NET preamble 

```log
[2024-09-06 18:49:36.011][00001][------][Information]  Elastic Distribution of OpenTelemetry .NET: 1.0.0-alpha.6.1
```

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

The `describe` command is great to validate the init-container ran and exposed
all the necessary environment variables.

```bash
kubectl describe pod my-dotnet-application -n my-dotnet-ns
```

You can use `exec` to inspect the container to see if it matches your expectations.
```log
kubectl exec my-dotnet-application -n my-dotnet-ns -- ls -la /otel-auto-instrumentation-dotnet
```



