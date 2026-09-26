// Foundation placeholder host for T-001.
// Intentionally has no endpoints, no authentication, no DI registrations,
// and no business functionality. Its only purpose is to prove that the
// Api project builds against the Application layer reference only.
var builder = WebApplication.CreateBuilder(args);

var app = builder.Build();

app.Run();
