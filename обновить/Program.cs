using Microsoft.Win32;
using Newtonsoft.Json.Linq;
using System.Diagnostics;
using System.IO.Compression;
using System.Net;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;


[assembly: AssemblyCompany("Processor")]
[assembly: AssemblyProduct("FreeAi Services Updater")]
[assembly: AssemblyVersion("1.3.0.0")]
[assembly: AssemblyFileVersion("1.3.0.0")]
[assembly: AssemblyInformationalVersion("1.3.0")]

class Program
{
    const string CurrentVersion = "1.3";
    const string AppName = "FreeAiServicesUpdaterApp";

    static void Main(string[] args)
    {
        bool silent = false;

        if (args.Length > 0 && args[0] == "--stop")
        {
            RemoveFromStartup();
            return;
        }

        if (args.Length > 0 && args[0] == "--start")
        {
            using (RegistryKey key = Registry.CurrentUser.OpenSubKey("Software\\Microsoft\\Windows\\CurrentVersion\\Run"))
            {
                if (key?.GetValue(AppName) == null)
                {
                    AddToStartup();
                }
            }

            silent = true;
            RunUpdateProcess(silent);
            return;
        }

        if (args.Length > 0 && args[0] == "--silent")
        {
            silent = true;
            RunUpdateProcess(silent);
            return;
        }

        // Обычный режим — интерактивный
        RunUpdateProcess(silent);
    }

    static string GetCurrentManifestVersion(bool silent)
    {
        string currentDirectory = Path.GetDirectoryName(Process.GetCurrentProcess().MainModule.FileName);
        string manifestPath = Path.Combine(currentDirectory, "manifest.json");
        if (!File.Exists(manifestPath))
        {
            if (!silent) Console.WriteLine("Файл manifest.json не найден.");
            return null;
        }
        try
        {
            string json = File.ReadAllText(manifestPath);
            JObject manifest = JObject.Parse(json);
            return manifest["version"]?.ToString();
        }
        catch (Exception ex)
        {
            if (!silent) Console.WriteLine("Ошибка чтения manifest.json: " + ex.Message);
            return null;
        }
    }

    static string GetLatestRepoVersion(string repoName, bool silent)
    {
        try
        {
            string apiUrl = $"https://api.github.com/repos/Processori7/{repoName}/contents/manifest.json";
            var request = (HttpWebRequest)WebRequest.Create(apiUrl);
            request.UserAgent = "UpdaterApp";

            using (var response = (HttpWebResponse)request.GetResponse())
            using (var reader = new StreamReader(response.GetResponseStream()))
            {
                string json = reader.ReadToEnd();
                JObject content = JObject.Parse(json);

                // Получаем содержимое файла в Base64
                string base64Content = content["content"]?.ToString();

                if (string.IsNullOrEmpty(base64Content))
                {
                    if (!silent) Console.WriteLine("Не удалось получить содержимое manifest.json.");
                    return null;
                }

                // Декодируем Base64
                byte[] data = Convert.FromBase64String(base64Content);
                string manifestJson = Encoding.UTF8.GetString(data);

                // Парсим JSON манифеста
                JObject manifest = JObject.Parse(manifestJson);
                return manifest["version"]?.ToString();
            }
        }
        catch (Exception ex)
        {
            if (!silent) Console.WriteLine($"Ошибка получения версии из репозитория {repoName}: {ex.Message}");
            return null;
        }
    }

    static void RunUpdateProcess(bool silent)
    {
        if (!silent)
        {
            Console.WriteLine("Запуск программы...");
        }

        string appPath = Process.GetCurrentProcess().MainModule.FileName;
        string currentDirectory = Path.GetDirectoryName(appPath);

        CheckForUpdates(silent);

        string folderName = ".git";
        string folderPath = Path.Combine(currentDirectory, folderName);

        string manifestType = GetManifestType(silent);
        string currentManifestVersion = GetCurrentManifestVersion(silent);

        string repoName = (manifestType == "chrome")
            ? "FreeAiChromeSidebar"
            : "FreeAi_Yandex_Opera_Ext";

        string latestVersion = GetLatestRepoVersion(repoName, silent);

        if (!string.IsNullOrEmpty(currentManifestVersion) && !string.IsNullOrEmpty(latestVersion))
        {
            if (currentManifestVersion != latestVersion)
            {
                if (!silent)
                {
                    Console.WriteLine($"Обнаружена новая версия: {latestVersion}. Текущая: {currentManifestVersion}");
                }

                // Пытаемся обновиться через git, если есть, иначе через архив
                if (IsGitInstalled() && Directory.Exists(folderPath))
                {
                    try
                    {
                        if (!silent) Console.WriteLine("Выполняется git pull...");
                        ExecuteCommand("git pull", silent);
                        if (!silent) Console.WriteLine("Репозиторий успешно обновлён.");
                    }
                    catch (Exception ex)
                    {
                        if (!silent) Console.WriteLine($"Ошибка при git pull: {ex.Message}. Перехожу к загрузке архива...");

                        DownloadArchive(silent);
                    }
                }
                else
                {
                    DownloadArchive(silent);
                }
            }
            else
            {
                if (!silent) Console.WriteLine("Расширение уже актуально.");
            }
        }
        else
        {
            if (!silent) Console.WriteLine("Не удалось сравнить версии. Выполняется стандартное обновление...");
            DownloadArchive(silent);
        }

        if (!silent)
        {
            Console.WriteLine("Обновление расширения завершено.");
            //Console.ReadKey();
        }
    }

    static void CheckForUpdates(bool silent)
    {
        try
        {
            string apiUrl = "https://api.github.com/repos/Processori7/update/releases/latest";

            Console.WriteLine("Проверяю обновления через GitHub API...");

            var request = (HttpWebRequest)WebRequest.Create(apiUrl);
            request.UserAgent = "UpdaterApp"; // Обязательное поле для GitHub API

            using (var response = (HttpWebResponse)request.GetResponse())
            using (var reader = new StreamReader(response.GetResponseStream()))
            {
                string json = reader.ReadToEnd();
                JObject release = JObject.Parse(json);

                string latestVersion = release["tag_name"]?.ToString();

                if (latestVersion != CurrentVersion)
                {
                    JArray assets = (JArray)release["assets"];
                    foreach (JObject asset in assets)
                    {
                        string fileName = asset["name"]?.ToString();
                        string downloadUrl = asset["browser_download_url"]?.ToString();

                        if (fileName == "FreeAiServicesUpdateApp.exe")
                        {
                            if (silent)
                            {
                                UpdateApplication(downloadUrl);
                            }
                            else
                            {
                                Console.WriteLine($"Доступно обновление: {CurrentVersion} → {latestVersion}");
                                Console.WriteLine("Хотите скачать и запустить обновление? (Y/N)");
                                var key = Console.ReadKey().Key;
                                if (key == ConsoleKey.Y || key == ConsoleKey.Enter)
                                {
                                    UpdateApplication(downloadUrl);
                                }
                                else
                                {
                                    Console.WriteLine("\nОбновление программы пропущено.");
                                }
                            }

                            break; // Один нужный файл нашли
                        }
                    }
                }
                else
                {
                    Console.WriteLine("Вы используете актуальную версию.");
                }
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine("Ошибка проверки обновлений через API: " + ex.Message);
        }
    }

    static void UpdateApplication(string downloadUrl)
    {
        try
        {
            string updateExe = Path.Combine(Path.GetTempPath(), "FreeAiServicesUpdateApp.exe");
            using (var webClient = new WebClient())
            {
                webClient.DownloadFile(downloadUrl, updateExe);
            }
            Process.Start(updateExe);
        }
        catch (Exception ex)
        {
            //Console.WriteLine("Ошибка при скачивании или запуске обновления: " + ex.Message);
        }
    }

    static void AddToStartup()
    {
        try
        {
            string appPath = Process.GetCurrentProcess().MainModule.FileName;
            using (RegistryKey key = Registry.CurrentUser.OpenSubKey("Software\\Microsoft\\Windows\\CurrentVersion\\Run", true))
            {
                key.SetValue(AppName, $"\"{appPath}\" --start");
                Console.WriteLine("Программа добавлена в автозагрузку.");
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine("Ошибка добавления в автозагрузку: " + ex.Message);
        }
    }

    static void RemoveFromStartup()
    {
        try
        {
            using (RegistryKey key = Registry.CurrentUser.OpenSubKey("Software\\Microsoft\\Windows\\CurrentVersion\\Run", true))
            {
                if (key.GetValue(AppName) != null)
                {
                    key.DeleteValue(AppName);
                    Console.WriteLine("Программа удалена из автозагрузки.");
                }
                else
                {
                    Console.WriteLine("Программа не найдена в автозагрузке.");
                }
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine("Ошибка удаления из автозагрузки: " + ex.Message);
        }
    }

    static string GetManifestType(bool silent)
    {
        string currentDirectory = Path.GetDirectoryName(Process.GetCurrentProcess().MainModule.FileName);
        string manifestPath = Path.Combine(currentDirectory, "manifest.json");

        if (!File.Exists(manifestPath))
        {
            if (silent) Console.WriteLine("Файл manifest.json не найден.");
            return "default";
        }

        try
        {
            string json = File.ReadAllText(manifestPath);
            if (json.Contains("\"side_panel\""))
            {
                if (!silent) Console.WriteLine("Версия Chrome");
                return "chrome";
            }
            else
            {
                if (!silent) Console.WriteLine("Версия Яндекс/Опера");
                return "yandex_opera";
            }
        }
        catch
        {
            if (!silent) Console.WriteLine("Ошибка чтения manifest.json");
            return "default";
        }
    }

    static void DownloadArchive(bool silent)
    {
        string currentDirectory = Path.GetDirectoryName(Process.GetCurrentProcess().MainModule.FileName);
        string manifestType = GetManifestType(silent);

        string url = "";
        string zipFile = "";
        string extractDir = "";

        switch (manifestType)
        {
            case "chrome":
                url = "https://github.com/Processori7/FreeAiChromeSidebar/archive/refs/heads/master.zip";
                zipFile = Path.Combine(currentDirectory, "Freeaichromesidebar-master.zip");
                extractDir = Path.Combine(currentDirectory, "FreeAiChromeSidebar-master");
                break;
            default:
                url = "https://github.com/Processori7/FreeAi_Yandex_Opera_Ext/archive/refs/heads/master.zip";
                zipFile = Path.Combine(currentDirectory, "FreeAi_Yandex_Opera_Ext-master.zip");
                extractDir = Path.Combine(currentDirectory, "FreeAi_Yandex_Opera_Ext-master");
                break;
        }

        try
        {
            using (WebClient client = new WebClient())
            {
                client.DownloadFile(new Uri(url), zipFile);
            }

            if (Directory.Exists(extractDir)) Directory.Delete(extractDir, true);
            using (ZipArchive archive = ZipFile.OpenRead(zipFile))
            {
                archive.ExtractToDirectory(extractDir);
            }

            string innerFolder = Path.Combine(extractDir, new DirectoryInfo(extractDir).Name);
            foreach (var file in Directory.GetFiles(innerFolder))
            {
                string destFile = Path.Combine(currentDirectory, Path.GetFileName(file));
                File.Copy(file, destFile, true);
            }

            Directory.Delete(extractDir, true);
            File.Delete(zipFile);
        }
        catch (Exception ex)
        {
            if (!silent) Console.WriteLine("Ошибка загрузки архива: " + ex.Message);
        }
    }

    static bool IsGitInstalled()
    {
        try
        {
            var processInfo = new ProcessStartInfo("git", "--version")
            {
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            using (var process = Process.Start(processInfo))
            {
                process.WaitForExit();
                return process.ExitCode == 0;
            }
        }
        catch
        {
            return false;
        }
    }

    static void ExecuteCommand(string command, bool silent)
    {
        var processInfo = new ProcessStartInfo("cmd.exe", "/c " + command)
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        using (var process = Process.Start(processInfo))
        {
            process.WaitForExit();
            if (!silent)
            {
                string output = process.StandardOutput.ReadToEnd();
                string error = process.StandardError.ReadToEnd();
                if (!string.IsNullOrEmpty(output)) Console.WriteLine(output);
                if (!string.IsNullOrEmpty(error)) Console.WriteLine("Ошибка: " + error);
            }
        }
    }
}