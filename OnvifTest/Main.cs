using System.ServiceModel.Channels;
using System.ServiceModel;
using Onvif.Security;
using DeviceClient;
using Imaging;
using MediaService;
using PtzService;
using Emgu.CV;

namespace OnvifTest
{
    public partial class Main : Form
    {
        ImagingPortClient? _imaging;
        PTZClient? _ptz;
        string? _token;
        VideoCapture? _videoCapture;
        string ip = "192.168.1.88";
        public Main()
        {
            InitializeComponent();
            textBox1.Text = ip;
        }

        private async void Form1_Load(object sender, EventArgs e)
        {
            try
            {
                await ReConnect();
            }
            catch (Exception)
            {

            }
        }

        private async Task ReConnect()
        {
            if (_videoCapture is not null)
            {
                _videoCapture.Stop();
            }
            var port = 8080;
            var user = "admin";
            var pass = "12345678h";
            _imaging = await CreateImagingClientAsync($"{ip}:{port}", user, pass);
            _ptz = await CreatePTZClientAsync($"{ip}:{port}", user, pass);
            var media = await CreateMediaClientAsync($"{ip}:{port}", user, pass);
            var profiles = await media.GetProfilesAsync();
            _token = profiles.Profiles[0].token;
            var mediaUri = (await media.GetStreamUriAsync(new StreamSetup(), _token)).Uri;
            _videoCapture = new VideoCapture(mediaUri);
            _videoCapture.ImageGrabbed += OnImageGrabbed;
            _videoCapture.Start();
        }

        private void OnImageGrabbed(object? sender, EventArgs e)
        {
            var frame = new Mat();
            try
            {
                if (_videoCapture!.Retrieve(frame) && !frame.IsEmpty)
                {
                    pictureBox1.Image = frame.ToBitmap();
                }
            }
            finally
            {
                frame.Dispose();
                GC.Collect();
            }
        }

        static async Task<ImagingPortClient> CreateImagingClientAsync(string host, string username, string password)
        {
            var binding = CreateBinding();
            var device = await CreateDeviceClientAsync(new Uri($"http://{host}/onvif/device_service"), username, password);
            var caps = await device.GetCapabilitiesAsync(new CapabilityCategory[] { CapabilityCategory.Imaging });
            var imaging = new ImagingPortClient(binding, new EndpointAddress(new Uri(caps.Capabilities.Imaging.XAddr)));

            var time_shift = await GetDeviceTimeShift(device);
            imaging.ChannelFactory.Endpoint.EndpointBehaviors.Clear();
            imaging.ChannelFactory.Endpoint.EndpointBehaviors.Add(new SoapSecurityHeaderBehavior(username, password, time_shift));

            // Connectivity Test
            await imaging.OpenAsync();

            return imaging;
        }

        static async Task<MediaClient> CreateMediaClientAsync(string host, string username, string password)
        {
            var binding = CreateBinding();
            var device = await CreateDeviceClientAsync(new Uri($"http://{host}/onvif/device_service"), username, password);
            var caps = await device.GetCapabilitiesAsync(new CapabilityCategory[] { CapabilityCategory.Media });
            var media = new MediaClient(binding, new EndpointAddress(new Uri(caps.Capabilities.Media.XAddr)));

            var time_shift = await GetDeviceTimeShift(device);
            media.ChannelFactory.Endpoint.EndpointBehaviors.Clear();
            media.ChannelFactory.Endpoint.EndpointBehaviors.Add(new SoapSecurityHeaderBehavior(username, password, time_shift));

            // Connectivity Test
            await media.OpenAsync();

            return media;
        }

        static async Task<PTZClient> CreatePTZClientAsync(string host, string username, string password)
        {
            var binding = CreateBinding();
            var device = await CreateDeviceClientAsync(new Uri($"http://{host}/onvif/device_service"), username, password);
            var caps = await device.GetCapabilitiesAsync(new CapabilityCategory[] { CapabilityCategory.PTZ });
            var ptz = new PTZClient(binding, new EndpointAddress(new Uri(caps.Capabilities.PTZ.XAddr)));

            var time_shift = await GetDeviceTimeShift(device);
            ptz.ChannelFactory.Endpoint.EndpointBehaviors.Clear();
            ptz.ChannelFactory.Endpoint.EndpointBehaviors.Add(new SoapSecurityHeaderBehavior(username, password, time_shift));

            // Connectivity Test
            await ptz.OpenAsync();

            return ptz;
        }

        static async Task<DeviceClient.DeviceClient> CreateDeviceClientAsync(Uri uri, string username, string password)
        {
            var binding = CreateBinding();
            var endpoint = new EndpointAddress(uri);
            var device = new DeviceClient.DeviceClient(binding, endpoint);
            var time_shift = await GetDeviceTimeShift(device);

            device = new DeviceClient.DeviceClient(binding, endpoint);
            device.ChannelFactory.Endpoint.EndpointBehaviors.Clear();
            device.ChannelFactory.Endpoint.EndpointBehaviors.Add(new SoapSecurityHeaderBehavior(username, password, time_shift));

            // Connectivity Test
            await device.OpenAsync();

            return device;
        }

        static System.ServiceModel.Channels.Binding CreateBinding()
        {
            var binding = new CustomBinding();
            var textBindingElement = new TextMessageEncodingBindingElement
            {
                MessageVersion = MessageVersion.CreateVersion(EnvelopeVersion.Soap12, AddressingVersion.None)
            };
            var httpBindingElement = new HttpTransportBindingElement
            {
                AllowCookies = true,
                MaxBufferSize = int.MaxValue,
                MaxReceivedMessageSize = int.MaxValue
            };

            binding.Elements.Add(textBindingElement);
            binding.Elements.Add(httpBindingElement);

            return binding;
        }

        static async Task<TimeSpan> GetDeviceTimeShift(DeviceClient.DeviceClient device)
        {
            var utc = (await device.GetSystemDateAndTimeAsync()).UTCDateTime;
            var dt = new System.DateTime(utc.Date.Year, utc.Date.Month, utc.Date.Day,
                              utc.Time.Hour, utc.Time.Minute, utc.Time.Second);
            return dt - System.DateTime.UtcNow;
        }

        private async void Button1_Click(object sender, EventArgs e)
        {
            if (_imaging is null) return;
            await _imaging.MoveAsync(_token, new FocusMove
            {
                Relative = new RelativeFocus
                {
                    Distance = 0.1f,
                    //Speed = 5f,
                    //SpeedSpecified = true
                }
            });
        }

        private async void Button2_Click(object sender, EventArgs e)
        {
            if (_imaging is null) return;
            await _imaging.MoveAsync(_token, new FocusMove
            {
                Relative = new RelativeFocus
                {
                    Distance = -0.1f,
                    //Speed = 5f,
                    //SpeedSpecified = true
                }
            });
        }

        private async void Button3_Click(object sender, EventArgs e)
        {
            if (_ptz is null) return;
            await _ptz.RelativeMoveAsync(_token, new PTZVector
            {
                //PanTilt = new PtzService.Vector2D
                //{
                //    x = 0.1f,
                //    y = 0.1f
                //},
                Zoom = new PtzService.Vector1D
                {
                    x = 1f
                }
            }, new PtzService.PTZSpeed
            {
                //PanTilt = new PtzService.Vector2D
                //{
                //    x = 0.1f,
                //    y = 0.1f
                //},
                Zoom = new PtzService.Vector1D
                {
                    x = 0f
                }
            });
            await Task.Delay(200);
            await _ptz.StopAsync(_token, true, true);
        }

        private async void Button4_Click(object sender, EventArgs e)
        {
            if (_ptz is null) return;
            await _ptz.RelativeMoveAsync(_token, new PTZVector
            {
                //PanTilt = new PtzService.Vector2D
                //{
                //    x = 0.1f,
                //    y = 0.1f
                //},
                Zoom = new PtzService.Vector1D
                {
                    x = -1f
                }
            }, new PtzService.PTZSpeed
            {
                //PanTilt = new PtzService.Vector2D
                //{
                //    x = 0.1f,
                //    y = 0.1f
                //},
                Zoom = new PtzService.Vector1D
                {
                    x = 0f
                }
            });
            await Task.Delay(200);
            await _ptz.StopAsync(_token, true, true);
        }

        private async void Button5_Click(object sender, EventArgs e)
        {
            ip = textBox1.Text;
            try
            {
                await ReConnect();
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.ToString());
            }
        }
    }
}