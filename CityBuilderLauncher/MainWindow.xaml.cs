using System;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using System.Windows;

namespace CityBuilderLauncher
{
    enum LauncherStatus
    {
        Ready,
        Failed,
        Downloading,
        Updating
    }

    /// <summary>
    /// Логика взаимодействия для MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        private const string VERSION_FILE_LINK = "https://www.dropbox.com/s/8ecry999psc09f8/Version.txt?dl=1";
        private const string GAME_ZIP_LINK = "https://www.dropbox.com/s/5zezo7kg1dr0zq7/Build.zip?dl=1";

        private string rootPath;
        private string versionFile;
        private string gameZip;
        private string gameExe;

        private LauncherStatus status;
        internal LauncherStatus Status
        {
            get => status;
            set
            {
                status = value;
                switch (status)
                {
                    case LauncherStatus.Ready:
                        PlayButton.Content = "Играть";
                        break;
                    case LauncherStatus.Failed:
                        PlayButton.Content = "Ошибка -- Попробуйте еще раз";
                        break;
                    case LauncherStatus.Downloading:
                        PlayButton.Content = "Скачивание...";
                        break;
                    case LauncherStatus.Updating:
                        PlayButton.Content = "Обновление...";
                        break;
                }
            }
        }

        public MainWindow()
        {
            InitializeComponent();

            rootPath = Directory.GetCurrentDirectory();
            versionFile = Path.Combine(rootPath, "Version.txt");
            gameZip = Path.Combine(rootPath, "Build.zip");
            gameExe = Path.Combine(rootPath, "Build", "Yunga Moore.exe");
        }

        private void Window_ContentRendered(object sender, EventArgs e)
        {
            CheckForUpdates();
        }

        private async void CheckForUpdates()
        {
            if (File.Exists(versionFile))
            {
                Version localVersion = new Version(File.ReadAllText(versionFile));
                VersionText.Text = localVersion.ToString();

                try
                {
                    Version onlineVersion;

                    using (HttpClient client = new HttpClient())
                    {
                        var response = await client.GetStringAsync(VERSION_FILE_LINK);
                        onlineVersion = new Version(response);
                    }

                    if (onlineVersion.IsDifferent(localVersion))
                    {
                        await InstallGameFiles(true, onlineVersion);
                    }
                    else
                    {
                        Status = LauncherStatus.Ready;
                    }
                }
                catch (Exception e)
                {
                    Status = LauncherStatus.Failed;
                    MessageBox.Show($"Error checking for game updates: {e}");
                }
            }
            else
            {
                await InstallGameFiles(false, Version.zero);
            }
        }

        private async Task InstallGameFiles(bool isUpdate, Version onlineVersion)
        {
            try
            {
                if (isUpdate)
                {
                    Status = LauncherStatus.Updating;
                }
                else
                {
                    Status = LauncherStatus.Downloading;

                    using (HttpClient client = new HttpClient())
                    {
                        var response = await client.GetStringAsync(VERSION_FILE_LINK);
                        onlineVersion = new Version(response);
                    }
                }

                using (HttpClient client = new HttpClient())
                {
                    using (HttpResponseMessage response = await client.GetAsync(GAME_ZIP_LINK))
                    {
                        using (var stream = await response.Content.ReadAsStreamAsync())
                        {
                            using (Stream zip = File.OpenWrite(gameZip))
                            {
                                await stream.CopyToAsync(zip);
                            }
                        }
                    }
                    await DownloadGameCompletedCallback(onlineVersion.ToString());
                }
            }
            catch (Exception e)
            {
                Status = LauncherStatus.Failed;
                MessageBox.Show($"Error installing game files: {e}");
            }
        }

        private async Task DownloadGameCompletedCallback(string version)
        {
            try
            {
                string onlineVersion = version;
                await Task.Run(() => ZipFile.ExtractToDirectory(gameZip, rootPath, true));
                File.Delete(gameZip);

                await File.WriteAllTextAsync(versionFile, onlineVersion);

                VersionText.Text = onlineVersion;
                Status = LauncherStatus.Ready;
            }
            catch (Exception e)
            {
                Status = LauncherStatus.Failed;
                MessageBox.Show($"Error finishing download: {e}");
            }
        }

        private void PlayButton_Click(object sender, RoutedEventArgs e)
        {
            if (File.Exists(gameExe) && Status == LauncherStatus.Ready)
            {
                ProcessStartInfo startInfo = new ProcessStartInfo(gameExe);
                startInfo.WorkingDirectory = Path.Combine(rootPath, "Build");
                Process.Start(startInfo);

                Close();
            }
            else if (Status == LauncherStatus.Failed)
            {
                CheckForUpdates();
            }
        }
    }

    struct Version
    {
        internal static Version zero = new Version(0, 0, 0);

        private short major;
        private short minor;
        private short subMinor;

        internal Version(short major, short minor, short subMinor)
        {
            this.major = major;
            this.minor = minor;
            this.subMinor = subMinor;
        }

        internal Version(string version)
        {
            string[] versions = version.Split('.');

            if (versions.Length != 3)
            {
                major = 0;
                minor = 0;
                subMinor = 0;
                return;
            }

            major = short.Parse(versions[0]);
            minor = short.Parse(versions[1]);
            subMinor = short.Parse(versions[2]);
        }

        internal bool IsDifferent(Version other)
        {
            if (major != other.major)
                return true;
            else
            {
                if (minor != other.minor)
                    return true;
                else
                {
                    if (subMinor != other.subMinor)
                        return true;
                }
            }

            return false;
        }

        public override string ToString()
        {
            return $"{major}.{minor}.{subMinor}";
        }
    }
}
