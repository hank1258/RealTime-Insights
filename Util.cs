// 
// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license.
// 
// Microsoft Cognitive Services: http://www.microsoft.com/cognitive
// 
// Microsoft Cognitive Services Github:
// https://github.com/Microsoft/Cognitive
// 
// Copyright (c) Microsoft Corporation
// All rights reserved.
// 
// MIT License:
// Permission is hereby granted, free of charge, to any person obtaining
// a copy of this software and associated documentation files (the
// "Software"), to deal in the Software without restriction, including
// without limitation the rights to use, copy, modify, merge, publish,
// distribute, sublicense, and/or sell copies of the Software, and to
// permit persons to whom the Software is furnished to do so, subject to
// the following conditions:
// 
// The above copyright notice and this permission notice shall be
// included in all copies or substantial portions of the Software.
// 
// THE SOFTWARE IS PROVIDED ""AS IS"", WITHOUT WARRANTY OF ANY KIND,
// EXPRESS OR IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF
// MERCHANTABILITY, FITNESS FOR A PARTICULAR PURPOSE AND
// NONINFRINGEMENT. IN NO EVENT SHALL THE AUTHORS OR COPYRIGHT HOLDERS BE
// LIABLE FOR ANY CLAIM, DAMAGES OR OTHER LIABILITY, WHETHER IN AN ACTION
// OF CONTRACT, TORT OR OTHERWISE, ARISING FROM, OUT OF OR IN CONNECTION
// WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE SOFTWARE.
// 

using ServiceHelpers;
using Microsoft.ProjectOxford.Common;
using Microsoft.ProjectOxford.Face;
using Microsoft.ProjectOxford.Face.Contract;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Windows.Devices.Enumeration;
using Windows.Graphics.Display;
using Windows.Graphics.Imaging;
using Windows.Storage;
using Windows.Storage.Streams;
using Windows.UI.Popups;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Media.Imaging;
using Microsoft.WindowsAzure; // Namespace for CloudConfigurationManager 
using Microsoft.WindowsAzure.Storage; // Namespace for CloudStorageAccount 
using Microsoft.WindowsAzure.Storage.Blob; // Namespace for Blob storage types 
using Microsoft.Azure.Devices;

//using Microsoft.ServiceBus.Messaging;

using System.IO;
using Newtonsoft.Json;
//using Microsoft.Azure; // Namespace for CloudConfigurationManager
//using Microsoft.WindowsAzure.Storage; // Namespace for CloudStorageAccount
//using Microsoft.WindowsAzure.Storage.Blob; // Namespace for Blob storage types
namespace IntelligentKioskSample
{
    internal static class Util
    {
        public static string CapitalizeString(string s)
        {
            return string.Join(" ", s.Split(' ').Select(word => !string.IsNullOrEmpty(word) ? char.ToUpper(word[0]) + word.Substring(1) : string.Empty));
        }

        internal static async Task GenericApiCallExceptionHandler(Exception ex, string errorTitle)
        {
            string errorDetails = GetMessageFromException(ex);

            await new MessageDialog(errorDetails, errorTitle).ShowAsync();
        }

        internal static string GetMessageFromException(Exception ex)
        {
            string errorDetails = ex.Message;

            FaceAPIException faceApiException = ex as FaceAPIException;
            if (faceApiException?.ErrorMessage != null)
            {
                errorDetails = faceApiException.ErrorMessage;
            }

            Microsoft.ProjectOxford.Common.ClientException commonException = ex as Microsoft.ProjectOxford.Common.ClientException;
            if (commonException?.Error?.Message != null)
            {
                errorDetails = commonException.Error.Message;
            }

            return errorDetails;
        }

        internal static Face FindFaceClosestToRegion(IEnumerable<Face> faces, BitmapBounds region)
        {
            return faces?.Where(f => Util.AreFacesPotentiallyTheSame(region, f.FaceRectangle))
                                  .OrderBy(f => Math.Abs(region.X - f.FaceRectangle.Left) + Math.Abs(region.Y - f.FaceRectangle.Top)).FirstOrDefault();
        }

        internal static bool AreFacesPotentiallyTheSame(BitmapBounds face1, FaceRectangle face2)
        {
            return CoreUtil.AreFacesPotentiallyTheSame((int)face1.X, (int)face1.Y, (int)face1.Width, (int)face1.Height, face2.Left, face2.Top, face2.Width, face2.Height);
        }

        public static async Task ConfirmActionAndExecute(string message, Func<Task> action)
        {
            var messageDialog = new MessageDialog(message);

            messageDialog.Commands.Add(new UICommand("Yes", new UICommandInvokedHandler(async (c) => await action())));
            messageDialog.Commands.Add(new UICommand("Cancel", new UICommandInvokedHandler((c) => { })));

            messageDialog.DefaultCommandIndex = 1;
            messageDialog.CancelCommandIndex = 1;

            await messageDialog.ShowAsync();
        }

        public static async Task<IEnumerable<string>> GetAvailableCameraNamesAsync()
        {
            DeviceInformationCollection deviceInfo = await DeviceInformation.FindAllAsync(DeviceClass.VideoCapture);
            return deviceInfo.OrderBy(d => d.Name).Select(d => d.Name);
        }

        async public static Task<ImageSource> GetCroppedBitmapAsync(Func<Task<Stream>> originalImgFile, Microsoft.ProjectOxford.Common.Rectangle rectangle)
        {
            try
            {
                using (IRandomAccessStream stream = (await originalImgFile()).AsRandomAccessStream())
                {
                    return await GetCroppedBitmapAsync(stream, rectangle);
                }
            }
            catch
            {
                // default to no image if we fail to crop the bitmap
                return null;
            }
        }

        async public static Task<ImageSource> GetCroppedBitmapAsync(IRandomAccessStream stream, Microsoft.ProjectOxford.Common.Rectangle rectangle)
        {
            var pixels = await GetCroppedPixelsAsync(stream, rectangle);

            // Stream the bytes into a WriteableBitmap 
            WriteableBitmap cropBmp = new WriteableBitmap(rectangle.Width, rectangle.Height);
            cropBmp.FromByteArray(pixels);

            return cropBmp;
        }

        async private static Task<byte[]> GetCroppedPixelsAsync(IRandomAccessStream stream, Rectangle rectangle)
        {
            // Create a decoder from the stream. With the decoder, we can get  
            // the properties of the image. 
            BitmapDecoder decoder = await BitmapDecoder.CreateAsync(stream);

            // Create cropping BitmapTransform and define the bounds. 
            BitmapTransform transform = new BitmapTransform();
            BitmapBounds bounds = new BitmapBounds();
            bounds.X = (uint)rectangle.Left;
            bounds.Y = (uint)rectangle.Top;
            bounds.Height = (uint)rectangle.Height;
            bounds.Width = (uint)rectangle.Width;
            transform.Bounds = bounds;

            // Get the cropped pixels within the bounds of transform. 
            PixelDataProvider pix = await decoder.GetPixelDataAsync(
                BitmapPixelFormat.Bgra8,
                BitmapAlphaMode.Straight,
                transform,
                ExifOrientationMode.IgnoreExifOrientation,
                ColorManagementMode.ColorManageToSRgb);

            return pix.DetachPixelData();
        }

        public static void AddRange<T>(this IList<T> list, IEnumerable<T> items)
        {
            foreach (var item in items)
            {
                list.Add(item);
            }
        }
        /*
        private static string EVENT_HUB = "opendemoeh";
        private static string CONNECTION_STRING = "Endpoint=sb://opendemoeh.servicebus.windows.net/;SharedAccessKeyName=RootManageSharedAccessKey;SharedAccessKey=6VujxD91yRAdF0Y3Hyq4UHIlnIetUm2ZcfJJ7QcfF6g=";
        private static string BLOB_CONNECTION_STRING = "DefaultEndpointsProtocol=https;AccountName=opendemost;AccountKey=t6wYwnwoG1E6iuxSubks7OKlsJCRsELGyFRz7P65hPalOSJgO/BcrWuK2Q+vb9+5ZrPQAa5+STszN5aZYofgsA==";
        private static string FILE_URL = "https://opendemost.blob.core.windows.net/photo/";

        public static void sendFaceDetectedEvent(Face face, string path)
        {
            string fileName;
            if (Path.GetPathRoot(path) != null && Path.GetPathRoot(path) != "")
                fileName = path.Replace(Path.GetPathRoot(path), "").Replace("\\", "/");
            else
                fileName = path.Replace("\\", "/");

          //  System.Console.WriteLine("fileName:" + fileName);
            CloudStorageAccount storageAccount = CloudStorageAccount.Parse(BLOB_CONNECTION_STRING);
            CloudBlobClient blobClient = storageAccount.CreateCloudBlobClient();
            CloudBlobContainer container = blobClient.GetContainerReference("photo");
            CloudBlockBlob blockBlob = container.GetBlockBlobReference(fileName);
            using (var fileStream = System.IO.File.OpenRead(path))
            {
                blockBlob.UploadFromStreamAsync(fileStream);
                
            }

            Random rand = new Random();
            var eventHubClient = EventHubClient.CreateFromConnectionString(CONNECTION_STRING, EVENT_HUB);

            var dict = new Dictionary<string, string>();
            dict.Add("id", face.FaceId.ToString());
            dict.Add("gender", face.FaceAttributes.Gender);
            dict.Add("age", face.FaceAttributes.Age.ToString());
            dict.Add("date", DateTime.Now.ToString("yyyy-MM-dd HH:mm"));
            dict.Add("smile", face.FaceAttributes.Smile.ToString());
            dict.Add("glasses", face.FaceAttributes.Glasses.ToString());
            dict.Add("avgs", rand.Next(5, 8).ToString());
            dict.Add("avgrank", (3 + rand.NextDouble() * 1.5).ToString());
            dict.Add("path", FILE_URL + fileName);

            string json = JsonConvert.SerializeObject(dict, Formatting.Indented);
            eventHubClient.Send(new EventData(Encoding.UTF8.GetBytes(json)));



        }*/

    }
}
