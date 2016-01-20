using GoogleAnalyticsTracker.Simple;
using System;
using System.Collections.Generic;
using System.Configuration;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Web;
using System.Web.Caching;
using System.Xml.Serialization;


namespace ProxyFileHandler
{
    /// <summary>
    /// Summary description for Handler1
    /// </summary>
    public class Handler1 : HttpTaskAsyncHandler
    {

        //from https://github.com/maartenba/GoogleAnalyticsTracker
        public async static Task GoogleTrackAsync(string url){
            //setup tracker
            using (SimpleTracker tracker = new SimpleTracker(ConfigurationManager.AppSettings["FileHandler:GoogleTrackingCode"], ConfigurationManager.AppSettings["FileHandler:GoogleTrackingDomain"]))
            {
                //track the download etc
                //GoogleAnalyticsTracker.Core.TrackingResult trackerResult2 = await tracker.TrackEventAsync(ConfigurationManager.AppSettings["FileHandler:GoogleTrackingPageTitle"], url);
                var trackerResult = await tracker.TrackPageViewAsync(ConfigurationManager.AppSettings["FileHandler:GoogleTrackingPageTitle"], url);
            }
        }

        public override async Task ProcessRequestAsync(HttpContext context)
        {

            HttpResponse response = context.Response;

            string uri;

            Boolean trackImage = false;

            try
            {
                // Get the URL requested by the client (take the entire querystring at once
                //  to handle the case of the URL itself containing querystring parameters)
                uri = context.Request.Url.Query.Substring(1);
            }
            catch
            {
                response.Clear();
                context.Response.StatusCode = 404;
                context.Response.End();
                return;
            }


            // Get token, if applicable, and append to the request
            string token = getTokenFromConfigFile(uri);
            if (!String.IsNullOrEmpty(token))
            {
                if (uri.Contains("?"))
                    uri += "&token=" + token;
                else
                    uri += "?token=" + token;
            }


            //vars
            string filename = "";
            string copyright = "";
            string copyrightFilename = "";

            const string CopyrightPrefix = "Copyright by ";
            const string CreativeCommonsPrefix = "cc by ";

            if (uri.Contains("filename") || uri.Contains("copyright") || uri.Contains("ccby"))
            {
                Array array = uri.Split('&');
                foreach (string a in array)
                {
                    var t = a.Split('=');
                    if (t[0] == "filename")
                    {
                        filename = HttpUtility.UrlDecode(t[1]);
                    }
                    else if (t[0] == "copyright")
                    {
                        copyright = CopyrightPrefix + HttpUtility.UrlDecode(t[1]);
                        copyrightFilename = "icon-copyright.jpg";
                    }
                    else if (t[0] == "ccby")
                    {
                        copyright = CreativeCommonsPrefix + HttpUtility.UrlDecode(t[1]);
                        copyrightFilename = "icon-creativecommons.jpg";
                    }
                }
            }

            //test for directory going up to pass root path i.e ".."
            if (filename.Contains(".."))
            {
                response.Clear();
                context.Response.StatusCode = 404;
                context.Response.End();
                return;
            }


            // place in try to exit out gracefully.
            try
            {
                //test if no filename
                if (filename == null)
                {
                    response.End();
                    return;
                }

                //get extension and set mixtype , \\assume jpg
                string ext = "image/jpeg";
                if (filename.Split('.')[1] != null)
                {
                    ext = GetContentType(Path.GetExtension(filename));
                }
                else
                {
                    response.Clear();
                    context.Response.StatusCode = 404;
                    context.Response.End();
                    return;
                }

                //TEST For image types that can not have copyright on them.
                //grab known types from settings
                string[] applyCopyrightToImageTypes = ConfigurationManager.AppSettings["FileHandler:ApplyCopyrightToImageTypes"].Split(',');

                #region Apply Copyright
                //if can take copyright then do it.
                if (!string.IsNullOrEmpty(copyright) && applyCopyrightToImageTypes.Any(ext.Contains))
                {
                    //Create the image object from the path
                    Image imgPhoto = Image.FromFile(ConfigurationManager.AppSettings["FileHandler:FilePathRoot"] + @filename);

                    // Increase height of image by 5% for insertion of copyright info
                    int copyrightHeight = (int)Math.Round(imgPhoto.Height * 0.05, 0);

                    //Get the dimensions of imgPhoto
                    int phWidth = imgPhoto.Width;
                    int phHeight = imgPhoto.Height;
                    int phHeightWithCopyright = imgPhoto.Height + copyrightHeight;

                    //Create a new object from the imgPhoto
                    Bitmap bmPhoto = new Bitmap(phWidth, phHeightWithCopyright, PixelFormat.Format24bppRgb);
                    bmPhoto.SetResolution(72, 72);
                    Graphics grPhoto = Graphics.FromImage(bmPhoto);

                    // Add white space background for copyright info
                    grPhoto.FillRectangle(new SolidBrush(Color.White), 0, phHeight, phWidth, copyrightHeight);
                    
                    // Load copyright image components
                    Image imgCopyright = Image.FromFile(context.Server.MapPath("~/" + copyrightFilename));
                    
                    //Draws the imgPhoto to the graphics object position at (x-0, y=0) 100% of original
                    grPhoto.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
                    grPhoto.DrawImage(imgPhoto, new Rectangle(0, 0, phWidth, phHeight), 0, 0, phWidth, phHeight, GraphicsUnit.Pixel);
                    
                    //Set the above into a bitmap
                    Bitmap bmPhotoWithCopyright = new Bitmap(bmPhoto);
                    bmPhotoWithCopyright.SetResolution(imgPhoto.HorizontalResolution, imgPhoto.VerticalResolution);

                    Graphics grPhotoWithCopyright = Graphics.FromImage(bmPhotoWithCopyright);
                    
                    double copyrightScaleBy = (double)imgCopyright.Height / (double)copyrightHeight;
                    
                    trackImage = true;
                    int widthCopyrightLeft = (int)Math.Round(imgCopyright.Width / copyrightScaleBy, 0);
                    grPhotoWithCopyright.DrawImage(imgCopyright, new Rectangle(0, phHeight, widthCopyrightLeft, copyrightHeight), 0, 0, imgCopyright.Width, imgCopyright.Height, GraphicsUnit.Pixel);
                    
                    // Add copyright text
                    
                    // Consts based on copyright image dimensions based on original pixel sizes
                    const int CopyrightTextStartLeft = 70;

                    const double CopyrightTextPercentageHeightOfAvailableHeight = 0.5;

                    int copyrightTextSpaceHeightScaled = (int)Math.Round(imgCopyright.Height / copyrightScaleBy);

                    // Caculate height of copyright text
                    int copyrightTextHeight = (int)Math.Round(copyrightTextSpaceHeightScaled * CopyrightTextPercentageHeightOfAvailableHeight, 0);

                    Font font = new Font("Arial", copyrightTextHeight, GraphicsUnit.Pixel);
                    SolidBrush brush = new SolidBrush(Color.Black);
                    StringFormat stringFormat = new StringFormat()
                    {
                        Alignment = StringAlignment.Near
                    };

                    SizeF textSize = grPhotoWithCopyright.MeasureString(copyright, font);
                    
                    int top = phHeight + (int)Math.Round((copyrightTextSpaceHeightScaled - textSize.Height) / 2);
                    int left = (int)Math.Round((imgCopyright.Width + CopyrightTextStartLeft) / copyrightScaleBy);
                    grPhotoWithCopyright.DrawString(copyright, font, brush, new PointF(left, top), stringFormat);
                    
                    // Copy exif details across
                    foreach (var id in imgPhoto.PropertyIdList)
                    {
                        bmPhotoWithCopyright.SetPropertyItem(imgPhoto.GetPropertyItem(id));
                    }

                    //Finally, replace the original image with new
                    imgPhoto = bmPhotoWithCopyright;
                    grPhoto.Dispose();
                    grPhotoWithCopyright.Dispose();

                    ImageCodecInfo myImageCodecInfo;
                    Encoder myEncoder;
                    EncoderParameter myEncoderParameter;
                    EncoderParameters myEncoderParameters;

                    myImageCodecInfo = GetEncoderInfo("image/jpeg");

                    // Create an Encoder object based on the GUID

                    // for the Quality parameter category.
                    myEncoder = Encoder.Quality;
                    myEncoderParameters = new EncoderParameters(1);

                    // Save the bitmap as a JPEG file with quality level 100.
                    myEncoderParameter = new EncoderParameter(myEncoder, 100L);
                    myEncoderParameters.Param[0] = myEncoderParameter;

                    //Save as original name but concatenate _watermarked to it
                    //string newFilename = "watermarked_" + photoFilename;

                    /// LOCAL TEST imgPhoto.Save(basePath + "test", ImageFormat.Jpeg);


                    //SET META DATA IN IMAGE

                    //TODO!!!!!

                    //var filePath = @"\\fileservices02\GISData\Processed_Raster\Historic_Aerial_Imagery\" + @filename;
                    //get from config.
                    var filePath = ConfigurationManager.AppSettings["FileHandler:FilePathRoot"] + @filename;

                    response.Clear();
                    response.AddHeader("content-disposition", "attachment;filename=" + Path.GetFileName(filePath));
                    response.ContentType = ext;
                    ///response.BinaryWrite(File.ReadAllBytes(filePath));

                    //using memory stream to save image to response
                    byte[] fileContents;
                    using (MemoryStream memoryStream = new MemoryStream())
                    {
                        imgPhoto.Save(memoryStream, ImageFormat.Jpeg);
                        fileContents = memoryStream.ToArray();
                        response.BinaryWrite(fileContents);
                    }

                    //clean up
                    imgCopyright.Dispose();
                    imgPhoto.Dispose();

                    //track the download etc
                   //GoogleAnalyticsTracker.Core.TrackingResult trackerResult = await tracker.TrackPageViewAsync(ConfigurationManager.AppSettings["FileHandler:GoogleTrackingPageTitle"], context.Request.Url.AbsoluteUri);
                    if (trackImage)
                    await GoogleTrackAsync(context.Request.Url.PathAndQuery);
                }
                #endregion watermark
                else
                {
                    //IS NON IMAGE TYPE FILE SO COPYRIGHT NOT ABLE TO DO
                    //get from config.
                    var filePath = ConfigurationManager.AppSettings["FileHandler:FilePathRoot"] + @filename;

                    response.Clear();
                    response.AddHeader("content-disposition", "attachment;filename=" + Path.GetFileName(filePath));
                    response.ContentType = ext;
                    response.BinaryWrite(File.ReadAllBytes(filePath));

                    //track the download etc
                    // GoogleAnalyticsTracker.Core.TrackingResult trackerResult = await tracker.TrackPageViewAsync(ConfigurationManager.AppSettings["FileHandler:GoogleTrackingPageTitle"], context.Request.Url.AbsoluteUri);
                    if (trackImage) 
                        await GoogleTrackAsync(context.Request.Url.PathAndQuery);
                };

                
                if (HttpContext.Current != null)  HttpContext.Current.ApplicationInstance.CompleteRequest();

            }
            catch (Exception ex)
            {
                response.Clear();
                context.Response.StatusCode = 404;
                if (HttpContext.Current != null) HttpContext.Current.ApplicationInstance.CompleteRequest();
                throw new SystemException(@"General Error Trap", ex);
                //return;
            }
        }//end of process context

        public override bool IsReusable
        {
            get
            {
                return false;
            }
        }
        ///
        /// RE SETTING EXIF TAGS
        //My SetProperty code... (for ASCII property items only!)
        //Exif 2.2 requires that ASCII property items terminate with a null (0x00).
        private void SetProperty(ref PropertyItem prop, int iId, string sTxt)
        {
            int iLen = sTxt.Length + 1;
            byte[] bTxt = new Byte[iLen];
            for (int i = 0; i < iLen - 1; i++)
                bTxt[i] = (byte)sTxt[i];
            bTxt[iLen - 1] = 0x00;
            prop.Id = iId;
            prop.Type = 2;
            prop.Value = bTxt;
            prop.Len = iLen;
        }

        /// <summary>
        /// HELPS GET 100% GRT QUALITY!
        /// </summary>
        /// <param name="mimeType"></param>
        /// <returns></returns>
        private static ImageCodecInfo GetEncoderInfo(String mimeType)
        {
            int j;
            ImageCodecInfo[] encoders;
            encoders = ImageCodecInfo.GetImageEncoders();
            for (j = 0; j < encoders.Length; ++j)
            {
                if (encoders[j].MimeType == mimeType)
                    return encoders[j];
            }
            return null;
        }


        /// <summary>
        /// Helps with Mime Types
        /// <summary>
        /// Returns the content type based on the given file extension
        /// </summary>
        public static string GetContentType(string fileExtension)
        {
            var mimeTypes = new Dictionary<String, String>
            {
                {".bmp", "image/bmp"},
                {".gif", "image/gif"},
                {".jpeg", "image/jpeg"},
                {".jpg", "image/jpeg"},
                {".png", "image/png"},
                {".tif", "image/tiff"},
                {".tiff", "image/tiff"},
                {".doc", "application/msword"},
                {".docx", "application/vnd.openxmlformats-officedocument.wordprocessingml.document"},
                {".pdf", "application/pdf"},
                {".ppt", "application/vnd.ms-powerpoint"},
                {".pptx", "application/vnd.openxmlformats-officedocument.presentationml.presentation"},
                {".xlsx", "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet"},
                {".xls", "application/vnd.ms-excel"},
                {".csv", "text/csv"},
                {".xml", "text/xml"},
                {".txt", "text/plain"},
                {".zip", "application/zip"},
                {".ogg", "application/ogg"},
                {".mp3", "audio/mpeg"},
                {".wma", "audio/x-ms-wma"},
                {".wav", "audio/x-wav"},
                {".wmv", "audio/x-ms-wmv"},
                {".swf", "application/x-shockwave-flash"},
                {".avi", "video/avi"},
                {".mp4", "video/mp4"},
                {".mpeg", "video/mpeg"},
                {".mpg", "video/mpeg"},
                {".qt", "video/quicktime"}
            };

            // if the file type is not recognized, return "application/octet-stream" so the browser will simply download it
            return mimeTypes.ContainsKey(fileExtension) ? mimeTypes[fileExtension] : "application/octet-stream";
        }


        // Gets the token for a server URL from a configuration file
        // TODO: ?modify so can generate a new short-lived token from username/password in the config file
        private string getTokenFromConfigFile(string uri)
        {
            try
            {
                ProxyConfig config = ProxyConfig.GetCurrentConfig();
                if (config != null)
                    return config.GetToken(uri);
                else
                    throw new ApplicationException(
                        "Proxy.config file does not exist at application root, or is not readable.");
            }
            catch (InvalidOperationException)
            {
                // Proxy is being used for an unsupported service (proxy.config has mustMatch="true")
                HttpResponse response = HttpContext.Current.Response;
                response.StatusCode = (int)System.Net.HttpStatusCode.Forbidden;
                response.End();
            }
            catch (Exception e)
            {
                if (e is ApplicationException)
                    throw e;

                // just return an empty string at this point
                // -- may want to throw an exception, or add to a log file
            }

            return string.Empty;
        }



        [XmlRoot("ProxyConfig")]
        public class ProxyConfig
        {
            #region Static Members

            private static object _lockobject = new object();

            public static ProxyConfig LoadProxyConfig(string fileName)
            {
                ProxyConfig config = null;

                lock (_lockobject)
                {
                    if (System.IO.File.Exists(fileName))
                    {
                        XmlSerializer reader = new XmlSerializer(typeof(ProxyConfig));
                        using (System.IO.StreamReader file = new System.IO.StreamReader(fileName))
                        {
                            config = (ProxyConfig)reader.Deserialize(file);
                        }
                    }
                }

                return config;
            }

            public static ProxyConfig GetCurrentConfig()
            {
                ProxyConfig config = HttpRuntime.Cache["proxyConfig"] as ProxyConfig;
                if (config == null)
                {
                    string fileName = GetFilename(HttpContext.Current);
                    config = LoadProxyConfig(fileName);

                    if (config != null)
                    {
                        CacheDependency dep = new CacheDependency(fileName);
                        HttpRuntime.Cache.Insert("proxyConfig", config, dep);
                    }
                }

                return config;
            }

            public static string GetFilename(HttpContext context)
            {
                return context.Server.MapPath("~/proxy.config");
            }
            #endregion

            ServerUrl[] serverUrls;
            bool mustMatch;

            [XmlArray("serverUrls")]
            [XmlArrayItem("serverUrl")]
            public ServerUrl[] ServerUrls
            {
                get { return this.serverUrls; }
                set { this.serverUrls = value; }
            }

            [XmlAttribute("mustMatch")]
            public bool MustMatch
            {
                get { return mustMatch; }
                set { mustMatch = value; }
            }

            public string GetToken(string uri)
            {
                foreach (ServerUrl su in serverUrls)
                {
                    if (su.MatchAll && uri.StartsWith(su.Url, StringComparison.InvariantCultureIgnoreCase))
                    {
                        return su.Token;
                    }
                    else
                    {
                        if (String.Compare(uri, su.Url, StringComparison.InvariantCultureIgnoreCase) == 0)
                            return su.Token;
                    }
                }

                if (mustMatch)
                    throw new InvalidOperationException();

                return string.Empty;
            }
        }

        public class ServerUrl
        {
            string url;
            bool matchAll;
            string token;

            [XmlAttribute("url")]
            public string Url
            {
                get { return url; }
                set { url = value; }
            }

            [XmlAttribute("matchAll")]
            public bool MatchAll
            {
                get { return matchAll; }
                set { matchAll = value; }
            }

            [XmlAttribute("token")]
            public string Token
            {
                get { return token; }
                set { token = value; }
            }
        }
    }
}