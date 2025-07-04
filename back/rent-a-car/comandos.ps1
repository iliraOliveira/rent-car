az acr login --name acrlab007ilira --resource-group grEstudanteAzure   

docker tag bff-rent-car-local acrlab007ilira.azurecr.io/bff-rent-car-local:v1

docker push acrlab007ilira.azurecr.io/bff-rent-car-local:v1

az container env create --name bff-rent-car-local --resource-group grEstudanteAzure --location eastus2

az containerapp create --name bff-rent-car-local --resource-group grEstudanteAzure --environment bff-rent-car-local --image acrlab007ilira.azurecr.io/bff-rent-car-local:v1 --ingress 'external' --target-port 3001 --registry-server acrlab007ilira.azurecr.io
