var builder = DistributedApplication.CreateBuilder(args);



var api = builder.AddProject<Projects.AkTxSignatureDemo_Server>("api")
    .WithExternalHttpEndpoints();

var aktxsignaturedemo = builder.AddJavaScriptApp("aktxsignaturedemo", "../aktxsignaturedemo.client", "start")
    .WithReference(api)
    .WaitFor(api)
    .WithHttpEndpoint(env: "PORT")
    .WithExternalHttpEndpoints();

builder.Build().Run();
