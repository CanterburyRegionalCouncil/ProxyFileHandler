<%@ WebHandler Language="C#" Class="ProxyFileHandlers" %>
/*
  This proxy page does not have any security checks. It is highly recommended
  that a user deploying this proxy page on their web server, add appropriate
  security checks, for example checking request path, username/password, target
  url, etc.
*/
using System;
using System.Drawing;
using System.IO;
using System.Web;
using System.Collections.Generic;
using System.Text;
using System.Xml.Serialization;
using System.Web.Caching;
using System.Configuration;

using System.Drawing.Imaging;

/// <summary>
/// Forwards requests to an ArcGIS Server REST resource. Uses information in
/// the proxy.config file to determine properties of the server.
/// </summary>
public class ProxyFileHandlers : IHttpHandler
{

    public void ProcessRequest(HttpContext context)
    {

        HttpResponse response = context.Response;

        string uri;
        
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

        if (uri.Contains("filename"))
        {
            Array array = uri.Split('&');
            foreach (string a in array)
            {
                var t = a.Split('=');
                if (t[0] == "filename")
                {
                    filename = t[1];
                }
            }
        }

        //test for directory going up to pass root path i.e ".."
        if (uri.Contains(".."))
        {
            response.Clear();
            context.Response.StatusCode = 404;
            context.Response.End();
            return;
        }


        // place in try to exit out gracefully.
        try
        {
            //decode
            filename = HttpUtility.UrlDecode(filename);

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
                ext = GetContentType(string.Concat('.', filename.Split('.')[1]));
            }
            else
            {
                response.Clear();
                context.Response.StatusCode = 404;
                context.Response.End();
                return;
            }

            //add water mark stuff...
            var basePath = context.Server.MapPath("~/");

            //Create the image object from the path
            System.Drawing.Image imgPhoto = System.Drawing.Image.FromFile(ConfigurationManager.AppSettings["FileHandler:FilePathRoot"] + @filename);

            //Get the dimensions of imgPhoto
            int phWidth = imgPhoto.Width;
            int phHeight = imgPhoto.Height;

            //Create a new object from the imgPhoto
            Bitmap bmPhoto = new Bitmap(phWidth, phHeight, System.Drawing.Imaging.PixelFormat.Format24bppRgb);
            bmPhoto.SetResolution(72, 72);
            Graphics grPhoto = Graphics.FromImage(bmPhoto);

            //Load the watermark image saved as .bmp and set with background color of green (Alpha=0, R=106, G=125, B=106)
            System.Drawing.Image imgWatermark = System.Drawing.Image.FromFile(basePath + ConfigurationManager.AppSettings["FileHandler:CCBYImage"]);
            //System.Drawing.Image imgWatermark = System.Drawing.Image.FromFile(ConfigurationManager.AppSettings["FileHandler:FilePathRoot"] + ConfigurationManager.AppSettings["FileHandler:CCBYImage"]);

            //Size of imgWatermark
            int wmWidth = imgWatermark.Width;
            int wmHeight = imgWatermark.Height;

            //Draws the imgPhoto to the graphics object position at (x-0, y=0) 100% of original
            grPhoto.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
            grPhoto.DrawImage(imgPhoto, new Rectangle(0, 0, phWidth, phHeight), 0, 0, phWidth, phHeight, GraphicsUnit.Pixel);

            ////
            ////  THE FOLLOWING IS TO ADD TEXT _ NOT USED AS DOING IMAGE
            ////
            ////To maximize the size of the Logo text using seven different fonts
            //int[] sizes = new int[] { 16, 14, 12, 10, 8, 6, 4 };
            //Font crFont = null;
            //SizeF crSize = new SizeF();

            ////Determines the largest possible size of the font
            //for (int i = 0; i < 7; i++)
            //{
            //    crFont = new Font("arial", sizes[i], FontStyle.Bold);
            //    crSize = grPhoto.MeasureString(copyrightString, crFont);

            //    if ((ushort)crSize.Width < (ushort)phWidth)
            //        break;
            //}

            ////For photos with varying heights, determines a five percent position from bottom of image
            //int yPixlesFromBottom = (int)(phHeight * .05);
            //float yPosFromBottom = (int)((phHeight - yPixlesFromBottom) - (crSize.Height / 2));
            ////float xCenterOfImg = (phWidth / 2);

            //StringFormat StrFormat = new StringFormat();
            //StrFormat.Alignment = StringAlignment.Center;

            ////Create a brush with 60% Black (Alpha 153)
            //SolidBrush semiTransparBrushOne = new SolidBrush(Color.FromArgb(153, 0, 0, 0));

            ////Creates a shadow effect
            //grPhoto.DrawString(copyrightString, crFont, semiTransparBrushOne, new PointF(xCenterOfImg + 1, yPosFromBottom + 1), StrFormat);

            ////Create a brush with 60% White (Alpha 153)
            //SolidBrush semiTransparBrushTwo = new SolidBrush(Color.FromArgb(153, 255, 255, 255));

            ////Draws the same text on top of the previous
            //grPhoto.DrawString(copyrightString, crFont, semiTransparBrushTwo, new PointF(xCenterOfImg + 1, yPosFromBottom + 1), StrFormat);

            //Set the above into a bitmap
            Bitmap bmWatermark = new Bitmap(bmPhoto);
            bmWatermark.SetResolution(imgPhoto.HorizontalResolution, imgPhoto.VerticalResolution);

            Graphics grWatermark = Graphics.FromImage(bmWatermark);

            //Apply two color manipulations for the watermark
            ImageAttributes imageAttributes = new ImageAttributes();
            ColorMap colorMap = new ColorMap();

            colorMap.OldColor = Color.FromArgb(255, 0, 255, 0);
            colorMap.NewColor = Color.FromArgb(0, 0, 0, 0);
            System.Drawing.Imaging.ColorMap[] remapTable = { colorMap };

            imageAttributes.SetRemapTable(remapTable, ColorAdjustType.Bitmap);

            //Change the opacity of watermark by setting 3rd row, 3rd col to .3f
            float[][] colorMatrixElements = { 
            new float[] {1.0f, 0.0f, 0.0f, 0.0f, 0.0f},
            new float[] {0.0f, 1.0f, 0.0f, 0.0f, 0.0f},
            new float[] {0.0f, 0.0f, 1.0f, 0.0f, 0.0f},
            new float[] {0.0f, 0.0f, 0.0f, 0.3f, 0.0f},
            new float[] {0.0f, 0.0f, 0.0f, 0.0f, 1.0f}
            };

            ColorMatrix wmColorMatrix = new ColorMatrix(colorMatrixElements);

            imageAttributes.SetColorMatrix(wmColorMatrix, ColorMatrixFlag.Default, ColorAdjustType.Bitmap);

            //Draw the watermark in the bottom right hand corner of photo
            int xPosOfWm = ((phWidth - wmWidth) - 10);
            int yPosOfWm = ((phHeight - wmHeight) - 10);

            //Only PUT CC BY COPYRIGHT ON IMAGES THAT LARGE!!!
            if (phWidth > wmWidth)
            grWatermark.DrawImage(imgWatermark, new Rectangle(xPosOfWm, yPosOfWm, wmWidth, wmHeight), 0, 0, wmWidth, wmHeight, GraphicsUnit.Pixel, imageAttributes);

            //Finally, replace the original image with new
            imgPhoto = bmWatermark;
            grPhoto.Dispose();
            grWatermark.Dispose();

            ImageCodecInfo myImageCodecInfo;
            System.Drawing.Imaging.Encoder myEncoder;
            EncoderParameter myEncoderParameter;
            EncoderParameters myEncoderParameters;

            myImageCodecInfo = GetEncoderInfo("image/jpeg");

            // Create an Encoder object based on the GUID

            // for the Quality parameter category.
            myEncoder = System.Drawing.Imaging.Encoder.Quality;
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
            imgWatermark.Dispose();
            imgPhoto.Dispose();
            
            HttpContext.Current.ApplicationInstance.CompleteRequest();
            
        }
       catch (Exception ex)
        {
            response.Clear();
            context.Response.StatusCode = 404;          
            HttpContext.Current.ApplicationInstance.CompleteRequest();
            throw new SystemException(@"General Error Trap", ex);
            //return;
        }


    }

    ///
    /// RE SETTING EXIF TAGS
    //My SetProperty code... (for ASCII property items only!)
    //Exif 2.2 requires that ASCII property items terminate with a null (0x00).
    private void SetProperty(ref System.Drawing.Imaging.PropertyItem prop, int iId, string sTxt)
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

    public bool IsReusable
    {
        get
        {
            return false;
        }
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
