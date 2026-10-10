# Deploying Finnova to local Kubernetes

Runs the four backend services as separate Deployments:

| Service                       | Port | Exposed?            | DB connection key (app)                | Secret key (`finnova-db`)     |
|-------------------------------|------|---------------------|----------------------------------------|-------------------------------|
| `finnova-api-gateway`         | 5000 | Yes (NodePort 30500)| —                                      | —                             |
| `finnova-ua-service`          | 5010 | Internal only¹      | `ConnectionStrings:FinnovaConnection`  | `uaConnectionString`          |
| `finnova-accounts-service`    | 5020 | Internal only¹      | `ConnectionStrings:FinnovaConnection`  | `accountsConnectionString`    |
| `finnova-systemadmin-service` | 5030 | Internal only¹      | `ConnectionStrings:FinnovaConnection`  | `systemadminConnectionString` |

¹ Internal-only (`ClusterIP`), but you can reach any backend directly for testing via
`kubectl port-forward` without changing the manifest — see
[section 6](#6-test-each-backend-service-directly-port-forward).

Each data service pulls its OWN connection string from a dedicated key in the
`finnova-db` Secret, so the three services can point at different databases or use
different SQL logins independently. They are seeded pointing at the same `Finnova`
database; change any one of them without affecting the others.

The gateway reaches the backends by their Service DNS names, which match the cluster
addresses already set in `Finnova.ApiGateway/appsettings.json`. Only the gateway is
exposed to the host, mirroring the real architecture where it is the sole origin.

## Prerequisites

- **Docker Desktop with Kubernetes enabled**, using the **Kubeadm** cluster type
  (Docker Desktop → Settings → Kubernetes → Enable Kubernetes; the "Modify Kubernetes
  Cluster" dialog offers Kubeadm or kind — select **Kubeadm**). Kubeadm is a single-node
  cluster that shares Docker Desktop's image store, so locally built images are visible
  with no load step (see section 3). Wait until the Docker Desktop Kubernetes indicator
  is green, then confirm:
  ```powershell
  kubectl config use-context docker-desktop
  kubectl get nodes        # one node, STATUS Ready
  ```
- SQL Server running on your **host** machine, because the services point at
  `host.docker.internal`. It must:
  - Accept **SQL authentication** (a username/password login). Windows Integrated /
    `Trusted_Connection` cannot work from a Linux container — that is why the manifest
    uses a SQL login, not the original `(localdb)` / `Trusted_Connection` string.
  - Listen on TCP 1433 and allow remote connections through the firewall.
  - Have each target database created and migrated (one `Finnova` DB by default, or a
    separate DB per service if you split the connection strings).

## 1. Edit the host DB credentials

Open `k8s/finnova.yaml` and set the real SQL login(s) in the `finnova-db` Secret. There
is now one key per data service — edit them independently:

```yaml
stringData:
  uaConnectionString:          "Server=host.docker.internal,1433;Database=Finnova;User Id=<login>;Password=<password>;TrustServerCertificate=True;Encrypt=False"
  accountsConnectionString:    "Server=host.docker.internal,1433;Database=Finnova;User Id=<login>;Password=<password>;TrustServerCertificate=True;Encrypt=False"
  systemadminConnectionString: "Server=host.docker.internal,1433;Database=Finnova;User Id=<login>;Password=<password>;TrustServerCertificate=True;Encrypt=False"
```

To give a service its own database, change only its `Database=` (for example
`Database=Finnova_SystemAdmin`) and make sure that database exists and is migrated.

> `host.docker.internal` resolves to the host machine from Docker Desktop Kubernetes
> (Kubeadm), so the seeded connection strings reach your host SQL Server as-is.

## 2. Build the images (from the solution root)

```powershell
cd d:\Projects\Finnova\Finnova-API

docker build -t finnova-ua-service:local          -f Finnova.UAService/Dockerfile .
docker build -t finnova-accounts-service:local     -f Finnova.AccountsService/Dockerfile .
docker build -t finnova-systemadmin-service:local  -f Finnova.SystemAdminService/Dockerfile .
docker build -t finnova-api-gateway:local          -f Finnova.ApiGateway/Dockerfile .
```

## 3. Make the images visible to the cluster

The manifest sets `imagePullPolicy: Never`, so the cluster uses locally present images
instead of pulling from a registry.

**Docker Desktop Kubernetes (Kubeadm): nothing to do.** The Kubeadm single-node cluster
shares Docker Desktop's image store, so the `finnova-*:local` images you built in step 2
are already visible to the cluster. Skip straight to step 4.

You can confirm the tags exist in the shared store:

```powershell
docker image ls | Select-String finnova
```

> Note: this "no load step" behavior is specific to the **Kubeadm** cluster type. The
> **kind** cluster type runs its own containerd image store and would require a
> `kind load docker-image ...` for each image — we use Kubeadm, so that does not apply.

## 4. Deploy

```powershell
kubectl apply -f k8s/finnova.yaml
kubectl get pods -w        # wait until all 4 are Running and READY 1/1
kubectl get svc
```

## 5. Reach the gateway

**Option A — port-forward** (works on every cluster, keeps the URL at :5000 to match
the UI's current base URL):

```powershell
kubectl port-forward svc/finnova-api-gateway 5000:5000
# then:
curl http://localhost:5000/health
curl http://localhost:5000/api/ua/health
```

**Option B — NodePort** (Docker Desktop maps it to localhost):

```powershell
curl http://localhost:30500/health
```

## 6. Test each backend service directly (port-forward)

The three backend services are `ClusterIP` (internal only) and are not exposed to the
host, so normally you reach them through the gateway (`/api/ua/...`,
`/api/accounts/...`, `/api/systemadmin/...`). To hit a service directly for testing —
including its Swagger UI — port-forward it. This needs **no manifest change** and leaves
the internal-only architecture intact.

```powershell
# UA service
kubectl port-forward svc/finnova-ua-service 5010:5010
#   -> http://localhost:5010/health   http://localhost:5010/swagger

# Accounts service
kubectl port-forward svc/finnova-accounts-service 5020:5020
#   -> http://localhost:5020/health   http://localhost:5020/swagger

# SystemAdmin service
kubectl port-forward svc/finnova-systemadmin-service 5030:5030
#   -> http://localhost:5030/health   http://localhost:5030/swagger
```

Each `port-forward` runs in the foreground and holds that terminal open until you stop
it with Ctrl+C, so use a separate terminal per service (or forward only the one you are
testing). The manifest already sets `ASPNETCORE_ENVIRONMENT=Development`, so Swagger UI
is enabled on every service.

> **Auth still applies.** Port-forwarding only gives you network reachability; it does
> not bypass `[Authorize]`. SystemAdmin endpoints still require a valid JWT carrying the
> `SystemAdmin` role (issuer `Finnova`, audience `FinnovaClients`, signed with the
> `finnova-jwt` `signingKey`). In Swagger UI, click **Authorize** and paste the token.

## 7. Browse by hostname (Ingress)

Instead of `localhost:<port>`, you can reach the services by domain names like
`http://gateway.finnova.local` via an Ingress. The `finnova-ingress` resource is already
in `k8s/finnova.yaml`; it needs an ingress controller in the cluster plus local DNS
entries.

### 7.1 Install the ingress controller

Docker Desktop does not ship an ingress controller. Install ingress-nginx (pinned to a
specific release rather than a floating `main`):

```powershell
kubectl apply -f https://raw.githubusercontent.com/kubernetes/ingress-nginx/controller-v1.15.1/deploy/static/provider/cloud/deploy.yaml

# wait until the controller is ready
kubectl wait --namespace ingress-nginx `
  --for=condition=ready pod `
  --selector=app.kubernetes.io/component=controller `
  --timeout=120s
```

On Docker Desktop the controller Service gets `EXTERNAL-IP: localhost`, so it listens on
`localhost:80` with nothing further to do. If port 80 is taken, fall back to:
`kubectl port-forward -n ingress-nginx svc/ingress-nginx-controller 8080:80` and use
`:8080` in the URLs below.

> **Deprecation note (verify before relying on this long-term).** The
> `kubernetes/ingress-nginx` project was archived in March 2026 — no further releases or
> security fixes. `controller-v1.15.1` is the pinned version used here and works for
> local dev, but for STG/Prod pick a maintained controller (AWS Load Balancer Controller
> on EKS — see "Moving to AWS" — or a supported NGINX/Traefik distribution). Content was
> rephrased for compliance with licensing restrictions. Source:
> [ingress-nginx deploy docs](https://kubernetes.github.io/ingress-nginx/deploy/).

### 7.2 Add local DNS entries

`.local` names do not resolve on their own. Edit `C:\Windows\System32\drivers\etc\hosts`
**as Administrator** and add one line:

```
127.0.0.1  gateway.finnova.local ua.finnova.local accounts.finnova.local systemadmin.finnova.local
```

### 7.3 Apply and browse

`kubectl apply -f k8s/finnova.yaml` already created the Ingress. Then:

| Hostname                        | Routes to                     | Use for                              |
|---------------------------------|-------------------------------|--------------------------------------|
| `http://gateway.finnova.local`  | gateway (`:5000`)             | Normal entry point; mirrors Prod     |
| `http://ua.finnova.local`       | `finnova-ua-service` (`:5010`)| Individual UA service testing        |
| `http://accounts.finnova.local` | `finnova-accounts` (`:5020`)  | Individual Accounts service testing  |
| `http://systemadmin.finnova.local` | `finnova-systemadmin` (`:5030`) | Individual SystemAdmin testing   |

```powershell
curl http://gateway.finnova.local/health
curl http://gateway.finnova.local/api/ua/health
# individual services (bypass the gateway):
curl http://systemadmin.finnova.local/health
start http://systemadmin.finnova.local/swagger
```

> The direct `<svc>.finnova.local` hosts bypass the gateway and exist only for local
> individual-service testing. In STG/Prod the gateway is the sole public entry point —
> do **not** expose direct backend hostnames there (see
> [Promoting across environments](#promoting-across-environments-qa--stg--prod)).
> Auth still applies to every host.

## Troubleshooting

- **Pod `CrashLoopBackOff` or not READY** -> `kubectl logs deploy/finnova-ua-service`.
  A DB connection failure is the usual cause: check that service's key in the
  `finnova-db` Secret (`uaConnectionString` / `accountsConnectionString` /
  `systemadminConnectionString`), that SQL Server allows SQL auth + TCP 1433, and that
  the firewall permits it.
- **`ImagePullBackOff`** -> the image was not loaded into the cluster (step 3), or the
  tag does not match the manifest. With `imagePullPolicy: Never` the image must exist
  locally in the cluster's store.
- **Gateway returns 502 for `/api/ua/...`** -> the target backend pod is not READY yet,
  or the Service name does not match the address in `appsettings.json`. Confirm with
  `kubectl get svc` that the names are exactly `finnova-ua-service`,
  `finnova-accounts-service`, `finnova-systemadmin-service`.
- **Swagger not reachable** -> Swagger UI is only mapped when
  `ASPNETCORE_ENVIRONMENT=Development`. This manifest sets `Development` on all four
  services, so Swagger is enabled here. In STG/Prod the environment is `Production` and
  Swagger is intentionally off — flip the env value only on the service you want to
  inspect, and never leave it on in Prod.
- **`gateway.finnova.local` does not resolve / connection refused** -> the hosts-file line
  (section 7.2) is missing or the ingress controller is not ready. Check
  `kubectl get pods -n ingress-nginx` and `kubectl get ingress`.
- **Ingress returns 404 for a host** -> the `Host` header does not match a rule, or you
  are hitting the wrong port. Confirm the hostname spelling against the table in 7.3.

## Teardown

```powershell
kubectl delete -f k8s/finnova.yaml
# optional: remove the ingress controller too
kubectl delete -f https://raw.githubusercontent.com/kubernetes/ingress-nginx/controller-v1.15.1/deploy/static/provider/cloud/deploy.yaml
```

## Promoting across environments (QA / STG / Prod)

The same four Deployments + gateway run in every environment. **What changes between
environments is configuration, not the application topology** — connection strings,
secrets, the ASP.NET environment name, exposure, TLS, and replica counts. Keep one base
manifest and layer per-environment overrides on top; do **not** fork `finnova.yaml` four
ways by hand.

### What differs per environment

| Concern                | Local (this file)                  | QA                                   | STG                                   | Prod                                        |
|------------------------|------------------------------------|--------------------------------------|---------------------------------------|---------------------------------------------|
| `ASPNETCORE_ENVIRONMENT` | `Development` (Swagger on)        | `Staging` or `Development`           | `Staging`                             | `Production` (Swagger off)                  |
| DB connection strings  | `host.docker.internal` SQL login   | QA DB server/creds                   | STG DB server/creds                   | Prod DB server/creds                        |
| JWT signing key        | hard-coded dev key in Secret       | per-env Secret                       | per-env Secret                        | real secret from a vault/secret manager     |
| Exposure               | NodePort + `*.finnova.local` hosts | gateway host only                    | gateway host only                     | gateway host only, public DNS               |
| Direct backend hosts   | yes (testing convenience)          | optional, internal only              | no                                    | no                                          |
| TLS                    | none (HTTP)                        | optional                             | yes (cert)                            | yes (managed cert, HTTP->HTTPS redirect)    |
| Replicas               | 1                                  | 1                                    | 2+                                    | 2+ with HPA                                 |
| Image tag              | `:local`                           | immutable tag (e.g. git SHA)         | same SHA promoted                     | same SHA promoted                           |

Key rules that hold in every non-local environment:

- **Gateway is the only public entry point.** The three backends stay `ClusterIP`. Drop
  the `NodePort` on the gateway and the direct `*.finnova.local` Ingress hosts — those
  are local-only. QA/STG/Prod expose exactly one host (e.g. `api.qa.finnova.example`,
  `api.stg.finnova.example`, `api.finnova.example`) routed to the gateway.
- **Swagger off in Prod.** Only `Development`/`Staging` map Swagger. Leave Prod on
  `Production`.
- **Secrets come from a real store**, not inline `stringData`. See the AWS section for
  the Secrets Manager / SSM pattern; the same idea applies to any environment (sealed
  secrets, external-secrets operator, CSI driver).
- **Immutable image tags.** Replace `:local` + `imagePullPolicy: Never` with a registry
  image at a fixed tag (git SHA or semver) and `imagePullPolicy: IfNotPresent`. Promote
  the *same* built image from QA -> STG -> Prod; never rebuild per environment.

### Kustomize layout (base + overlays)

The manifests are split into a shared base plus one thin overlay per environment, so each
environment is a small diff rather than a copied file:

```
k8s/
  finnova.yaml                 # the original single-file manifest (still works; = local)
  base/
    kustomization.yaml         # lists the shared resources (no Secrets, no image tags)
    ua.yaml accounts.yaml systemadmin.yaml gateway.yaml ingress.yaml
  overlays/
    local/   # :local images, Development (Swagger on), NodePort 30500,
             #   *.finnova.local direct hosts, dev Secrets inline
    qa/      # registry image @ gitsha, Staging, gateway host only, namespace finnova-qa
    stg/     # TLS, Staging, replicas=2, namespace finnova-stg
    prod/    # TLS + HTTPS redirect, Production (Swagger off), replicas=2 + HPA,
             #   namespace finnova-prod
```

Apply an overlay (not the base) with `kubectl -k`:

```powershell
kubectl apply -k k8s/overlays/local    # Docker Desktop — equivalent to finnova.yaml
kubectl apply -k k8s/overlays/qa        # or stg / prod
```

Preview what an overlay renders without applying:

```powershell
kubectl kustomize k8s/overlays/prod
```

What each overlay owns:

- **Base** holds the environment-agnostic Deployments, Services (all `ClusterIP`), and a
  gateway-only Ingress with a placeholder host. It defaults `ASPNETCORE_ENVIRONMENT` to
  `Production` (Swagger off) — the safe default — and carries image **names** without a
  tag. Base is never applied directly.
- **local** reproduces the single `finnova.yaml`: `:local` images with
  `imagePullPolicy: Never`, `Development` on every service, the gateway as `NodePort
  30500`, the direct `*.finnova.local` hosts for individual-service testing, and the dev
  Secrets inline (safe only because they target `host.docker.internal` with a throwaway
  key).
- **qa / stg / prod** set the registry image + immutable tag (`images:` — replace
  `<registry>`/`<gitsha>`), patch the gateway Ingress host (TLS on stg/prod), set the CORS
  origin, raise replicas (stg/prod), add an HPA (prod), and each lives in its own
  namespace. They do **not** add the direct backend hosts — the gateway is the sole entry
  point. Promote the *same* image tag qa -> stg -> prod; never rebuild per environment.

> **Secrets are placeholders in qa/stg/prod.** Each has a `secrets-placeholder.yaml` with
> `REPLACE_FROM_SECRET_STORE` values and keys named exactly as the Deployments consume
> them (`finnova-db`: `uaConnectionString` / `accountsConnectionString` /
> `systemadminConnectionString`; `finnova-jwt`: `signingKey`). Replace that file with your
> real secret source (External Secrets Operator or the Secrets Store CSI driver backed by
> AWS Secrets Manager / SSM — see "Moving to AWS"). Never commit real credentials.

> All four overlays were validated to render with `kubectl kustomize` (the Kustomize build
> is bundled into `kubectl`). The original single `finnova.yaml` is left in place and still
> works standalone — it is the flattened `local` overlay. Helm is a valid alternative if
> the team prefers templated values files instead of overlays.

## Moving to AWS (EKS)

The manifests are standard Kubernetes, so the application objects move to AWS largely
unchanged. What changes is the surrounding platform: a managed cluster (EKS), a managed
database (RDS), real secret storage, a cloud load balancer, and a container registry.

### Mapping from local to AWS

| Local (Docker Desktop)                         | AWS equivalent                                             |
|------------------------------------------------|------------------------------------------------------------|
| Docker Desktop Kubernetes (Kubeadm)            | **EKS** cluster (managed control plane)                    |
| Host SQL Server via `host.docker.internal`     | **RDS for SQL Server** (private subnet; reach by endpoint) |
| `:local` images, `imagePullPolicy: Never`      | **ECR** images at an immutable tag, pulled by nodes        |
| ingress-nginx + `*.finnova.local` hosts file   | **AWS Load Balancer Controller** + an NLB/ALB + Route 53   |
| inline `finnova-jwt` / `finnova-db` Secrets    | **Secrets Manager / SSM** via External Secrets or CSI      |
| NodePort / port-forward                        | LoadBalancer Service or ALB Ingress, public DNS + ACM TLS  |

### Steps (high level)

1. **Registry (ECR).** Create a repo per image, build and push with an immutable tag:
   ```powershell
   aws ecr get-login-password --region <region> | docker login --username AWS --password-stdin <acct>.dkr.ecr.<region>.amazonaws.com
   docker build -t <acct>.dkr.ecr.<region>.amazonaws.com/finnova-api-gateway:<gitsha> -f Finnova.ApiGateway/Dockerfile .
   docker push <acct>.dkr.ecr.<region>.amazonaws.com/finnova-api-gateway:<gitsha>
   # repeat for ua / accounts / systemadmin
   ```
   In the manifests, replace `image: finnova-*:local` + `imagePullPolicy: Never` with the
   ECR image reference and `imagePullPolicy: IfNotPresent`.

2. **Cluster (EKS).** Provision with `eksctl` or Terraform (prefer Terraform/IaC so the
   cluster is reproducible). Managed node group or Fargate; connect `kubectl` with
   `aws eks update-kubeconfig --name <cluster> --region <region>`.

3. **Database (RDS for SQL Server).** Create an RDS instance in private subnets, in the
   same VPC as the cluster, with a security group that allows TCP 1433 from the node/pod
   security group. Replace `host.docker.internal` in the connection strings with the RDS
   endpoint. The services already read `ConnectionStrings:FinnovaConnection`, so only the
   value changes. Run EF migrations against RDS before/with the first deploy.

4. **Secrets.** Store the JWT signing key and DB connection strings in **AWS Secrets
   Manager** (or SSM Parameter Store). Surface them as Kubernetes Secrets with the
   **External Secrets Operator** or the **Secrets Store CSI driver**, so no credentials
   live in Git. The Deployments keep consuming them via `secretKeyRef` exactly as now.

5. **Ingress / load balancer.** Install the **AWS Load Balancer Controller** and expose
   the **gateway only** through an ALB Ingress (or an NLB in front of ingress-nginx). Put
   the public hostname (e.g. `api.finnova.example`) in **Route 53** and terminate TLS with
   an **ACM** certificate on the load balancer. Backends stay `ClusterIP`. Example ALB
   annotations live in the ingress-nginx AWS docs referenced in section 7.1.

6. **Scaling & resilience.** Set `resources.requests/limits` on each container, raise
   `replicas` (2+) for the gateway and busy services, and add a `HorizontalPodAutoscaler`.
   Spread across AZs via the managed node group.

7. **CI/CD.** Build once, push to ECR with the git SHA, then promote that same image tag
   through the QA -> STG -> Prod overlays (see "Promoting across environments"). Never
   rebuild per environment.

> **Scope note.** The application manifests (Deployments, Services, the gateway, the
> Ingress rules) carry over to EKS with only image, host, TLS, replica, and secret-source
> changes. Everything else above (EKS, RDS, ECR, ACM, Route 53, IAM, VPC) is AWS platform
> setup that lives in your infrastructure-as-code, not in `finnova.yaml`. This section is
> the migration map, not a turnkey script — the exact IaC depends on your AWS account and
> networking standards.

