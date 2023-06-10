﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Realms;
using Realms.Sync;
using System.Threading.Tasks;
using Windows.UI.ViewManagement;
using Windows.UI;
using Windows.UI.Xaml.Media.Imaging;
using Windows.ApplicationModel.Resources;
using Windows.Security.ExchangeActiveSyncProvisioning;
using Windows.Networking;
using Windows.Networking.Connectivity;
using Windows.Storage.Streams;
using Windows.UI.Xaml.Media;
using System.Collections.ObjectModel;
using MongoDB.Bson.Serialization;

// The Blank Page item template is documented at https://go.microsoft.com/fwlink/?LinkId=402352&clcid=0x409

namespace DigiSign_Realm
{
    /// <summary>
    /// An empty page that can be used on its own or navigated to within a Frame.
    /// </summary>
    public sealed partial class MainPage : Page
    {
        DispatcherTimer _disTimer;
        int _currentIndex = 0;
        int _currentTimer = 0;
        string _realmAppID = "";
        string _realmAPIKey = "";
        string _realmEmail = "";
        string _realmPassword = "";
        string _realmPartition = "";
        string _RealmHostingUrl = "";
        string _WebsiteHomepage = "";

        public Realms.Sync.App app { get; set; }
        public Realms.Sync.User user { get; set; }
        public Realms.Sync.PartitionSyncConfiguration config { get; set; }
        public Realm realm { get; set; }
        public string partition { get; set; }
        private IQueryable<Models.Sign> allSigns = null;
        private ObservableCollection<Models.Sponsor> sponsorsToDisplay = null;
        private ObservableCollection<Models.Menu> menuToDisplay = null;



        public MainPage()
        {
            this.InitializeComponent();

            var view = ApplicationView.GetForCurrentView();
            view.TitleBar.BackgroundColor = Color.FromArgb(255, 11, 140, 26);
            view.TitleBar.ButtonBackgroundColor = Color.FromArgb(255, 11, 140, 26);
            view.TitleBar.ButtonForegroundColor = Colors.White;
            view.TitleBar.ButtonPressedForegroundColor = Color.FromArgb(255, 11, 140, 26);
            view.TitleBar.ButtonPressedBackgroundColor = Colors.White;
            view.TitleBar.ButtonHoverBackgroundColor = Colors.White;
            view.TitleBar.ButtonHoverForegroundColor = Color.FromArgb(255, 11, 140, 26);

            var resources = new ResourceLoader("Resources");
            _realmAppID = resources.GetString("AutoProvsionRealmAppID");
            _realmAPIKey = resources.GetString("AutoProvisionRealmAPIKey");
            _realmEmail = resources.GetString("AutoProvisionRealmEmail");
            _realmPassword = resources.GetString("AutoProvisionRealmPassword");
            _RealmHostingUrl = resources.GetString("RealmHostingUrl");
            _WebsiteHomepage = resources.GetString("WebsiteHomepage");
            txt_connection.Text = "..." + _realmAPIKey.Substring(_realmAPIKey.Length - 5) + " @ " + _realmAppID;

            string qruri = "https://chart.googleapis.com/chart?cht=qr&chs=300x300&chl=" + Uri.EscapeDataString(_RealmHostingUrl);
            BitmapImage imageSource = new BitmapImage(new Uri(qruri));
            img_qr.Source = imageSource;

            _disTimer = new DispatcherTimer();
            _disTimer.Tick += _disTimer_Tick;
            EnableSync();
        }

        private async Task SyncSigns()
        {
            try
            {
                var bsonval = await user.Functions.CallAsync("getMyPartitions", txt_deviceID.Text, txt_ipaddr.Text);
                _realmPartition = bsonval.ToString();

                //Get all signs (the realm should already be filtered)
                allSigns = realm.All<Models.Sign>().OrderBy(sign => sign.Order);

                await realm.SyncSession.WaitForDownloadAsync();
            }
            catch (Exception ex)
            {
                txt_error.Text = ex.ToString();
                Console.WriteLine(ex.ToString());
            }
        }

        public async Task EnableSync()
        {
            // https://stackoverflow.com/questions/31746613/how-do-i-get-a-unique-identifier-for-a-device-within-windows-10-universal
            var deviceInformation = new EasClientDeviceInformation();
            string deviceID = deviceInformation.FriendlyName.ToString() + "_" + deviceInformation.Id.ToString();
            txt_deviceID.Text = deviceID;

            // https://stackoverflow.com/questions/33770429/how-do-i-find-the-local-ip-address-on-a-win-10-uwp-project
            foreach (HostName localHostName in NetworkInformation.GetHostNames())
            {
                if (localHostName.IPInformation != null)
                {
                    if (localHostName.Type == HostNameType.Ipv4)
                    {
                        txt_ipaddr.Text = localHostName.ToString();
                        break;
                    }
                }
            }

            // connect
            app = Realms.Sync.App.Create(_realmAppID);
            user = await app.LogInAsync(Realms.Sync.Credentials.EmailPassword(_realmEmail, _realmPassword));

            // get auto provision details
            var bsonval = await user.Functions.CallAsync("getMyPartitions", deviceID, txt_ipaddr.Text);
            _realmPartition = bsonval.ToString();

            if (_realmPartition.Length < 2)
            {
                txt_error.Text = "Registration API returned empty or null.";
                await Task.Delay(TimeSpan.FromSeconds(15));
                EnableSync();
            }
            else
            {
                txt_error.Text = "Syncing signs...";

                config = new Realms.Sync.PartitionSyncConfiguration("GLOBAL", user);

                realm = await Realm.GetInstanceAsync(config);

                await SyncSigns();

                var token = realm.All<Models.Sign>().SubscribeForNotifications((sender, changes) => {
                    //This essentially just tells it to pull again on changes by re-setting the index
                    allSigns.OrderBy(sign => sign.Order);
                    _currentIndex = 0;
                });

                DisplayNext();
            }
        }

        private void _disTimer_Tick(object sender, object e)
        {
            DisplayNext();
        }

        async void DisplaySponsors()
        {
            ctr_configstack.Visibility = Visibility.Collapsed;
            ctr_view_image.Visibility = Visibility.Collapsed;
            ctr_view_media.Visibility = Visibility.Collapsed;
            ctr_view_text.Visibility = Visibility.Collapsed;
            ctr_view_web.Visibility = Visibility.Collapsed;
            ctr_sponsors.Visibility = Visibility.Visible;
            ctr_menu.Visibility = Visibility.Collapsed;

            // get six random sponsors
            var allSponsors = realm.All<Models.Sponsor>().ToList();
            sponsorsToDisplay = new ObservableCollection<Models.Sponsor>(allSponsors.OrderBy(s => Guid.NewGuid()).Take(10).ToList());
            lv_sponsor.ItemsSource = sponsorsToDisplay;
            lv_sponsor.UpdateLayout();

            _currentIndex = 0;
            //DisplayNext();
        }

        async Task DisplayMenu(string menuID)
        {
            ctr_configstack.Visibility = Visibility.Collapsed;
            ctr_view_image.Visibility = Visibility.Collapsed;
            ctr_view_media.Visibility = Visibility.Collapsed;
            ctr_view_text.Visibility = Visibility.Collapsed;
            ctr_view_web.Visibility = Visibility.Collapsed;
            ctr_sponsors.Visibility = Visibility.Collapsed;
            ctr_menu.Visibility = Visibility.Visible;

            string qruri = "https://chart.googleapis.com/chart?cht=qr&chs=300x300&chl=" + Uri.EscapeDataString(_WebsiteHomepage + "/" + menuID.ToLower());
            BitmapImage imageSource = new BitmapImage(new Uri(qruri));
            img_menu_qr.Source = imageSource;

            var allMenus = realm.All<Models.Menu>().ToList();
            menuToDisplay = new ObservableCollection<Models.Menu>(allMenus.Where(m => m.SoldAtID == menuID).OrderBy(m => m.Name).ToList());
            txt_menu_heading.Text = menuToDisplay.FirstOrDefault().SoldAtName;
            var groupedMenu = menuToDisplay.GroupBy(m => m.CategoryName).ToList();

            sp_menu.Children.Clear();


            foreach (var g in groupedMenu)
            {
                if ((g.Key != "Utility") && (g.Key != "DNI") && (g.Key != "Other"))
                {
                    sp_menu.Children.Clear();

                    var gs = new StackPanel();

                    var t = new RichEditBox();
                    t.Background = new SolidColorBrush(Colors.Gray);
                    t.Document.SetText(Windows.UI.Text.TextSetOptions.None, g.Key);
                    t.FontSize = 96;
                    t.TextAlignment = TextAlignment.Center;
                    gs.Children.Add(t);

                    foreach (var m in g)
                    {
                        var n = new TextBlock();
                        n.Text = m.Name + " - " + m.PriceFormatted;
                        n.FontSize = 72;
                        n.TextAlignment = TextAlignment.Center;

                        gs.Children.Add(n);
                    }
                    sp_menu.Children.Add(gs);
                    sp_menu.UpdateLayout();
                    _currentTimer = 5;
                    await Task.Delay(TimeSpan.FromSeconds(_currentTimer));
                    
                }
            }

            _currentIndex = 0;
        }

        async void DisplayNext()
        {

            if (allSigns == null)
            {
                ctr_configstack.Visibility = Visibility.Visible;
                ctr_view_image.Visibility = Visibility.Collapsed;
                ctr_view_media.Visibility = Visibility.Collapsed;
                ctr_view_text.Visibility = Visibility.Collapsed;
                ctr_view_web.Visibility = Visibility.Collapsed;
                ctr_sponsors.Visibility = Visibility.Collapsed;
                ctr_menu.Visibility = Visibility.Collapsed;

                txt_error.Text = "Registration success; screen list is null.";
            }
            else if (allSigns.Count() == 0)
            {
                ctr_configstack.Visibility = Visibility.Visible;
                ctr_view_image.Visibility = Visibility.Collapsed;
                ctr_view_media.Visibility = Visibility.Collapsed;
                ctr_view_text.Visibility = Visibility.Collapsed;
                ctr_view_web.Visibility = Visibility.Collapsed;
                ctr_sponsors.Visibility = Visibility.Collapsed;
                ctr_menu.Visibility = Visibility.Collapsed;

                txt_error.Text = "Registration success; screen count is " + allSigns.Count().ToString();
            }
            else
            {
                var partitions = _realmPartition.Split(",").ToList();

                if (_currentIndex == allSigns.Count())
                {
                    // we looped all the way around so reset
                    _currentIndex = 0;
                    // see if our partitions changed

                    await SyncSigns();

                    // only on last cycle run sponsors code
                    if (partitions.Contains("sponsors"))
                    {
                        ctr_configstack.Visibility = Visibility.Collapsed;
                        ctr_view_image.Visibility = Visibility.Collapsed;
                        ctr_view_media.Visibility = Visibility.Collapsed;
                        ctr_view_text.Visibility = Visibility.Collapsed;
                        ctr_view_web.Visibility = Visibility.Collapsed;
                        ctr_sponsors.Visibility = Visibility.Visible;
                        ctr_menu.Visibility = Visibility.Collapsed;
                        DisplaySponsors();
                        _currentTimer = 20;
                        await Task.Delay(TimeSpan.FromSeconds(_currentTimer));
                        _currentIndex = 0;
                    }

                    // only on last cycle run menu code
                    //if (partitions.Contains("menu_"))
                    if(partitions.Any(p=>p.StartsWith("menu_")))
                    {
                        ctr_configstack.Visibility = Visibility.Collapsed;
                        ctr_view_image.Visibility = Visibility.Collapsed;
                        ctr_view_media.Visibility = Visibility.Collapsed;
                        ctr_view_text.Visibility = Visibility.Collapsed;
                        ctr_view_web.Visibility = Visibility.Collapsed;
                        ctr_sponsors.Visibility = Visibility.Collapsed;
                        ctr_menu.Visibility = Visibility.Visible;

                        string menuID = "";
                        foreach(string p in partitions)
                        {
                            if(p.StartsWith("menu_"))
                            {
                                menuID = p.Split("_")[1];
                            }
                        }
                        try
                        {
                            _disTimer.Stop();
                            await DisplayMenu(menuID);
                        }
                        catch (Exception ex)
                        {
                            _disTimer.Stop();
                            sp_menu.Children.Clear();
                            _currentIndex = 0;
                        }
                    }
                }

                Models.Sign s = allSigns.OrderBy(sign => sign.Order).ElementAt(_currentIndex);

                if (s.Type == "hide")
                {
                    _currentIndex++;
                    DisplayNext();
                } 
                // cant figure out linq syntax for this 
                else if (!partitions.Contains(s.Feed)) {
                    _currentIndex++;
                    DisplayNext();
                }
                else
                {
                    ctr_configstack.Visibility = Visibility.Collapsed;
                    ctr_view_web.Visibility = Visibility.Collapsed;
                    ctr_view_image.Visibility = Visibility.Collapsed;
                    ctr_view_text.Visibility = Visibility.Collapsed;
                    ctr_view_media.Visibility = Visibility.Collapsed;
                    ctr_sponsors.Visibility = Visibility.Collapsed;
                    ctr_menu.Visibility = Visibility.Collapsed;

                    if (s.Type == "web")
                    {
                        ctr_view_web.Visibility = Visibility.Visible;

                        ctr_view_web.Visibility = Visibility.Visible;
                        ctr_view_web.Navigate(new Uri(s.URI));

                    }
                    if (s.Type == "video")
                    {
                        ctr_view_media.Visibility = Visibility.Visible;

                        ctr_view_media.Source = new Uri(s.URI);

                    }
                    else if (s.Type == "image")
                    {
                        ctr_view_image.Visibility = Visibility.Visible;
                        
                        BitmapImage imageSource = new BitmapImage(new Uri(s.URI));
                        ctr_view_image.Width = imageSource.DecodePixelHeight = (int)this.ActualWidth;
                        ctr_view_image.Source = imageSource;
                    }
                    else if ((s.Type == "base64image") || (s.Type == "base64"))
                    {
                        ctr_view_image.Visibility = Visibility.Visible;

                        var ims = new InMemoryRandomAccessStream();
                        var bytes = Convert.FromBase64String(s.Text);
                        var dataWriter = new DataWriter(ims);
                        dataWriter.WriteBytes(bytes);
                        await dataWriter.StoreAsync();
                        ims.Seek(0);
                        var imageSource = new BitmapImage();
                        imageSource.SetSource(ims);
                        ctr_view_image.Width = imageSource.DecodePixelHeight = (int)this.ActualWidth;
                        ctr_view_image.Source = imageSource;

                    }
                    else if (s.Type == "text")
                    {
                        ctr_view_text.Visibility = Visibility.Visible;

                        ctr_view_text.Document.SetText(Windows.UI.Text.TextSetOptions.None, s.Text);
                    }

                    _currentTimer = Convert.ToInt32(s.Duration);
                    _disTimer.Interval = new TimeSpan(0, 0, _currentTimer);
                    _disTimer.Start();

                    _currentIndex++;
                }
            }

        }

        private void ctr_view_image_ImageFailed(object sender, ExceptionRoutedEventArgs e)
        {
            _currentIndex++;
            DisplayNext();
        }

        private void ctr_view_web_NavigationFailed(object sender, WebViewNavigationFailedEventArgs e)
        {
            _currentIndex++;
            DisplayNext();
        }
    }
}
