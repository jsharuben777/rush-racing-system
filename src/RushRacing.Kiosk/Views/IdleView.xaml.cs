using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.IO;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using QRCoder;

namespace RushRacing.Kiosk.Views
{
    /// <summary>
    /// Interaction logic for IdleView.xaml
    /// </summary>
    public partial class IdleView : UserControl
    {
        public IdleView()
        {
            InitializeComponent();
        }

        public void SetBookingUrl(string bookingUrl)
        {
            BookingUrlText.Text = bookingUrl;

            using var generator = new QRCodeGenerator();
            using var data = generator.CreateQrCode(bookingUrl, QRCodeGenerator.ECCLevel.Q);
            var qrCode = new PngByteQRCode(data);
            var png = qrCode.GetGraphic(20);

            using var stream = new MemoryStream(png);
            var image = new BitmapImage();
            image.BeginInit();
            image.CacheOption = BitmapCacheOption.OnLoad;
            image.StreamSource = stream;
            image.EndInit();
            image.Freeze();

            QrCodeImage.Source = image;
        }
    }
}
