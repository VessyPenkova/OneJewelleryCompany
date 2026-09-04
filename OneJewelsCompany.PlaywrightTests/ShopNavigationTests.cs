using System.Diagnostics;

namespace OneJewelsCompany.PlaywrightTests
{
    [SetUpFixture]
    public class WebAppFixture
    {
        private Process? _webProcess;

        [OneTimeSetUp]
        public async Task StartWebApplication()
        {
            var webProjectPath = FindWebProject();

            var startInfo = new ProcessStartInfo
            {
                FileName = "dotnet",
                Arguments =
                    $"run --project \"{webProjectPath}\" --no-launch-profile --urls http://localhost:5108",
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true
            };

            _webProcess = new Process
            {
                StartInfo = startInfo
            };

            _webProcess.Start();

            await WaitForApplicationAsync("http://localhost:5108");
        }

        [OneTimeTearDown]
        public void StopWebApplication()
        {
            if (_webProcess == null || _webProcess.HasExited)
                return;

            _webProcess.Kill(entireProcessTree: true);
            _webProcess.Dispose();
        }

        private static string FindWebProject()
        {
            var directory = new DirectoryInfo(
                TestContext.CurrentContext.TestDirectory);

            while (directory != null)
            {
                var directPath = Path.Combine(
                    directory.FullName,
                    "OneJevelsCompany.Web.csproj");

                if (File.Exists(directPath))
                    return directPath;

                var nestedPath = Path.Combine(
                    directory.FullName,
                    "OneJevelsCompany.Web",
                    "OneJevelsCompany.Web.csproj");

                if (File.Exists(nestedPath))
                    return nestedPath;

                directory = directory.Parent;
            }

            throw new FileNotFoundException(
                "Could not locate OneJevelsCompany.Web.csproj.");
        }

        private static async Task WaitForApplicationAsync(string url)
        {
            using var client = new HttpClient();

            for (var attempt = 0; attempt < 30; attempt++)
            {
                try
                {
                    using var response = await client.GetAsync(url);

                    if (response.IsSuccessStatusCode)
                        return;
                }
                catch (HttpRequestException)
                {
                    // Application is still starting.
                }

                await Task.Delay(1000);
            }

            throw new InvalidOperationException(
                $"The Web application did not start at {url}.");
        }
    }
}