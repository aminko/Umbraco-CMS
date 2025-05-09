﻿using System;
using System.Globalization;
using Moq;
using Newtonsoft.Json;
using NUnit.Framework;
using Newtonsoft.Json.Linq;
using Umbraco.Core;
using Umbraco.Core.Composing;
using Umbraco.Core.Configuration.UmbracoSettings;
using Umbraco.Core.IO;
using Umbraco.Core.Logging;
using Umbraco.Core.Models;
using Umbraco.Core.Models.PublishedContent;
using Umbraco.Core.PropertyEditors;
using Umbraco.Core.PropertyEditors.ValueConverters;
using Umbraco.Core.Services;
using Umbraco.Tests.Components;
using Umbraco.Tests.TestHelpers;
using Umbraco.Web.Models;
using Umbraco.Web;
using Umbraco.Web.PropertyEditors;
using System.Text;
using Umbraco.Core.Security;

namespace Umbraco.Tests.PropertyEditors
{

    [TestFixture]
    public class ImageCropperTest
    {
        private const string CropperJson1 = "{\"focalPoint\": {\"left\": 0.96,\"top\": 0.80827067669172936},\"src\": \"/media/1005/img_0671.jpg\",\"crops\": [{\"alias\":\"thumb\",\"width\": 100,\"height\": 100,\"coordinates\": {\"x1\": 0.58729977382575338,\"y1\": 0.055768992440203169,\"x2\": 0,\"y2\": 0.32457553600198386}}]}";
        private const string CropperJson2 = "{\"focalPoint\": {\"left\": 0.98,\"top\": 0.80827067669172936},\"src\": \"/media/1005/img_0672.jpg\",\"crops\": [{\"alias\":\"thumb\",\"width\": 100,\"height\": 100,\"coordinates\": {\"x1\": 0.58729977382575338,\"y1\": 0.055768992440203169,\"x2\": 0,\"y2\": 0.32457553600198386}}]}";
        private const string CropperJson3 = "{\"focalPoint\": {\"left\": 0.5,\"top\": 0.5},\"src\": \"/media/1005/img_0672.jpg\",\"crops\": []}";
        private const string MediaPath = "/media/1005/img_0671.jpg";

        [Test]
        public void CanConvertImageCropperDataSetSrcToString()
        {
            //cropperJson3 - has no crops
            var cropperValue = CropperJson3.DeserializeImageCropperValue();
            var serialized = cropperValue.TryConvertTo<string>();
            Assert.IsTrue(serialized.Success);
            Assert.AreEqual("/media/1005/img_0672.jpg", serialized.Result);
        }

        [Test]
        public void CanConvertImageCropperDataSetJObject()
        {
            //cropperJson3 - has no crops
            var cropperValue = CropperJson3.DeserializeImageCropperValue();
            var serialized = cropperValue.TryConvertTo<JObject>();
            Assert.IsTrue(serialized.Success);
            Assert.AreEqual(cropperValue, serialized.Result.ToObject<ImageCropperValue>());
        }

        [Test]
        public void CanConvertImageCropperDataSetJsonToString()
        {
            var cropperValue = CropperJson1.DeserializeImageCropperValue();
            var serialized = cropperValue.TryConvertTo<string>();
            Assert.IsTrue(serialized.Success);
            Assert.IsTrue(serialized.Result.DetectIsJson());
            var obj = JsonConvert.DeserializeObject<ImageCropperValue>(CropperJson1, new JsonSerializerSettings {Culture = CultureInfo.InvariantCulture, FloatParseHandling = FloatParseHandling.Decimal});
            Assert.AreEqual(cropperValue, obj);
        }

        [TestCase(CropperJson1, CropperJson1, true)]
        [TestCase(CropperJson1, CropperJson2, false)]
        public void CanConvertImageCropperPropertyEditor(string val1, string val2, bool expected)
        {
            try
            {
                var container = RegisterFactory.Create();
                var composition = new Composition(container, new TypeLoader(), Mock.Of<IProfilingLogger>(), ComponentTests.MockRuntimeState(RuntimeLevel.Run));

                composition.WithCollectionBuilder<PropertyValueConverterCollectionBuilder>();

                Current.Factory = composition.CreateFactory();

                var logger = Mock.Of<ILogger>();
                var scheme = Mock.Of<IMediaPathScheme>();
                var config = Mock.Of<IContentSection>();

                var mediaFileSystem = new MediaFileSystem(Mock.Of<IFileSystem>(), config, scheme, logger);

                var imageCropperConfiguration = new ImageCropperConfiguration()
                {
                    Crops = new[]
                    {
                        new ImageCropperConfiguration.Crop()
                        {
                            Alias = "thumb",
                            Width = 100,
                            Height = 100
                        }
                    }
                };
                var dataTypeService = new TestObjects.TestDataTypeService(
                    new DataType(new ImageCropperPropertyEditor(
                        Mock.Of<ILogger>(),
                        mediaFileSystem,
                        Mock.Of<IContentSection>(),
                        Mock.Of<IDataTypeService>(),
                        Mock.Of<IFileStreamSecurityValidator>()))
                    {
                        Id = 1,
                        Configuration = imageCropperConfiguration
                    });

                var factory = new PublishedContentTypeFactory(Mock.Of<IPublishedModelFactory>(), new PropertyValueConverterCollection(Array.Empty<IPropertyValueConverter>()), dataTypeService);

                var converter = new ImageCropperValueConverter();
                var result = converter.ConvertSourceToIntermediate(null, factory.CreatePropertyType("test", 1), val1, false); // does not use type for conversion

                var resultShouldMatch = val2.DeserializeImageCropperValue();
                if (expected)
                {
                    Assert.AreEqual(resultShouldMatch, result);
                }
                else
                {
                    Assert.AreNotEqual(resultShouldMatch, result);
                }
            }
            finally
            {
                Current.Reset();
            }
        }

        [Test]
        public void GetCropUrl_CropAliasTest()
        {
            var urlString = MediaPath.GetCropUrl(new TestImageUrlGenerator(), imageCropperValue: CropperJson1, cropAlias: "Thumb", useCropDimensions: true);
            Assert.AreEqual(MediaPath + "?c=0.58729977382575338,0.055768992440203169,0,0.32457553600198386&w=100&h=100", urlString);
        }

        /// <summary>
        /// Test to ensure useCropDimensions is observed
        /// </summary>
        [Test]
        public void GetCropUrl_CropAliasIgnoreWidthHeightTest()
        {
            var urlString = MediaPath.GetCropUrl(new TestImageUrlGenerator(), imageCropperValue: CropperJson1, cropAlias: "Thumb", useCropDimensions: true, width: 50, height: 50);
            Assert.AreEqual(MediaPath + "?c=0.58729977382575338,0.055768992440203169,0,0.32457553600198386&w=100&h=100", urlString);
        }

        [Test]
        public void GetCropUrl_WidthHeightTest()
        {
            var urlString = MediaPath.GetCropUrl(new TestImageUrlGenerator(), imageCropperValue: CropperJson1, width: 200, height: 300);
            Assert.AreEqual(MediaPath + "?f=0.80827067669172936x0.96&w=200&h=300", urlString);
        }

        [Test]
        public void GetCropUrl_FocalPointTest()
        {
            var urlString = MediaPath.GetCropUrl(new TestImageUrlGenerator(), imageCropperValue: CropperJson1, cropAlias: "thumb", preferFocalPoint: true, useCropDimensions: true);
            Assert.AreEqual(MediaPath + "?f=0.80827067669172936x0.96&w=100&h=100", urlString);
        }

        [Test]
        public void GetCropUrlFurtherOptionsTest()
        {
            var urlString = MediaPath.GetCropUrl(new TestImageUrlGenerator(), imageCropperValue: CropperJson1, width: 200, height: 300, furtherOptions: "&filter=comic&roundedcorners=radius-26|bgcolor-fff");
            Assert.AreEqual(MediaPath + "?f=0.80827067669172936x0.96&w=200&h=300&filter=comic&roundedcorners=radius-26|bgcolor-fff", urlString);
        }

        /// <summary>
        /// Test that if a crop alias has been specified that doesn't exist the method returns null
        /// </summary>
        [Test]
        public void GetCropUrlNullTest()
        {
            var urlString = MediaPath.GetCropUrl(new TestImageUrlGenerator(), imageCropperValue: CropperJson1, cropAlias: "Banner", useCropDimensions: true);
            Assert.AreEqual(null, urlString);
        }

        /// <summary>
        /// Test the GetCropUrl method on the ImageCropDataSet Model
        /// </summary>
        [Test]
        public void GetBaseCropUrlFromModelTest()
        {
            var cropDataSet = CropperJson1.DeserializeImageCropperValue();
            var urlString = cropDataSet.GetCropUrl("thumb", new TestImageUrlGenerator());
            Assert.AreEqual("?c=0.58729977382575338,0.055768992440203169,0,0.32457553600198386&w=100&h=100", urlString);
        }

        /// <summary>
        /// Test the height ratio mode with predefined crop dimensions
        /// </summary>
        [Test]
        public void GetCropUrl_CropAliasHeightRatioModeTest()
        {
            var urlString = MediaPath.GetCropUrl(new TestImageUrlGenerator(), imageCropperValue: CropperJson1, cropAlias: "Thumb", useCropDimensions: true, ratioMode:ImageCropRatioMode.Height);
            Assert.AreEqual(MediaPath + "?c=0.58729977382575338,0.055768992440203169,0,0.32457553600198386&hr=1&w=100", urlString);
        }

        /// <summary>
        /// Test the height ratio mode with manual width/height dimensions
        /// </summary>
        [Test]
        public void GetCropUrl_WidthHeightRatioModeTest()
        {
            var urlString = MediaPath.GetCropUrl(new TestImageUrlGenerator(), imageCropperValue: CropperJson1, width: 300, height: 150, ratioMode:ImageCropRatioMode.Height);
            Assert.AreEqual(MediaPath + "?f=0.80827067669172936x0.96&hr=0.5&w=300", urlString);
        }

        /// <summary>
        /// Test the height ratio mode with width/height dimensions
        /// </summary>
        [Test]
        public void GetCropUrl_HeightWidthRatioModeTest()
        {
            var urlString = MediaPath.GetCropUrl(new TestImageUrlGenerator(), imageCropperValue: CropperJson1, width: 300, height: 150, ratioMode: ImageCropRatioMode.Width);
            Assert.AreEqual(MediaPath + "?f=0.80827067669172936x0.96&wr=2&h=150", urlString);
        }

        /// <summary>
        /// Test that if Crop mode is specified as anything other than Crop the image doesn't use the crop
        /// </summary>
        [Test]
        public void GetCropUrl_SpecifiedCropModeTest()
        {
            var urlStringMin = MediaPath.GetCropUrl(new TestImageUrlGenerator(), imageCropperValue: CropperJson1, width: 300, height: 150, imageCropMode: ImageCropMode.Min);
            var urlStringBoxPad = MediaPath.GetCropUrl(new TestImageUrlGenerator(), imageCropperValue: CropperJson1, width: 300, height: 150, imageCropMode: ImageCropMode.BoxPad);
            var urlStringPad = MediaPath.GetCropUrl(new TestImageUrlGenerator(), imageCropperValue: CropperJson1, width: 300, height: 150, imageCropMode: ImageCropMode.Pad);
            var urlString = MediaPath.GetCropUrl(new TestImageUrlGenerator(), imageCropperValue: CropperJson1, width: 300, height: 150, imageCropMode:ImageCropMode.Max);
            var urlStringStretch = MediaPath.GetCropUrl(new TestImageUrlGenerator(), imageCropperValue: CropperJson1, width: 300, height: 150, imageCropMode: ImageCropMode.Stretch);

            Assert.AreEqual(MediaPath + "?m=min&w=300&h=150", urlStringMin);
            Assert.AreEqual(MediaPath + "?m=boxpad&w=300&h=150", urlStringBoxPad);
            Assert.AreEqual(MediaPath + "?m=pad&w=300&h=150", urlStringPad);
            Assert.AreEqual(MediaPath + "?m=max&w=300&h=150", urlString);
            Assert.AreEqual(MediaPath + "?m=stretch&w=300&h=150", urlStringStretch);
        }

        /// <summary>
        /// Test for upload property type
        /// </summary>
        [Test]
        public void GetCropUrl_UploadTypeTest()
        {
            var urlString = MediaPath.GetCropUrl(new TestImageUrlGenerator(), width: 100, height: 270, imageCropMode: ImageCropMode.Crop, imageCropAnchor: ImageCropAnchor.Center);
            Assert.AreEqual(MediaPath + "?m=crop&a=center&w=100&h=270", urlString);
        }

        /// <summary>
        /// Test for preferFocalPoint when focal point is centered
        /// </summary>
        [Test]
        public void GetCropUrl_PreferFocalPointCenter()
        {
            const string cropperJson = "{\"focalPoint\": {\"left\": 0.5,\"top\": 0.5},\"src\": \"/media/1005/img_0671.jpg\",\"crops\": [{\"alias\":\"thumb\",\"width\": 100,\"height\": 100,\"coordinates\": {\"x1\": 0.58729977382575338,\"y1\": 0.055768992440203169,\"x2\": 0,\"y2\": 0.32457553600198386}}]}";

            var urlString = MediaPath.GetCropUrl(new TestImageUrlGenerator(), imageCropperValue: cropperJson, width: 300, height: 150, preferFocalPoint:true);
            Assert.AreEqual(MediaPath + "?m=defaultcrop&w=300&h=150", urlString);
        }

        /// <summary>
        /// Test to check if height ratio is returned for a predefined crop without coordinates and focal point in centre when a width parameter is passed
        /// </summary>
        [Test]
        public void GetCropUrl_PreDefinedCropNoCoordinatesWithWidth()
        {
            const string cropperJson = "{\"focalPoint\": {\"left\": 0.5,\"top\": 0.5},\"src\": \"/media/1005/img_0671.jpg\",\"crops\": [{\"alias\": \"home\",\"width\": 270,\"height\": 161}]}";

            var urlString = MediaPath.GetCropUrl(new TestImageUrlGenerator(), imageCropperValue: cropperJson, cropAlias: "home", width: 200);
            Assert.AreEqual(MediaPath + "?m=defaultcrop&hr=0.5962962962962962962962962963&w=200", urlString);
        }

        /// <summary>
        /// Test to check if height ratio is returned for a predefined crop without coordinates and focal point is custom when a width parameter is passed
        /// </summary>
        [Test]
        public void GetCropUrl_PreDefinedCropNoCoordinatesWithWidthAndFocalPoint()
        {
            const string cropperJson = "{\"focalPoint\": {\"left\": 0.4275,\"top\": 0.41},\"src\": \"/media/1005/img_0671.jpg\",\"crops\": [{\"alias\": \"home\",\"width\": 270,\"height\": 161}]}";

            var urlString = MediaPath.GetCropUrl(new TestImageUrlGenerator(), imageCropperValue: cropperJson, cropAlias: "home", width: 200);
            Assert.AreEqual(MediaPath + "?f=0.41x0.4275&hr=0.5962962962962962962962962963&w=200", urlString);
        }

        /// <summary>
        /// Test to check if crop ratio is ignored if useCropDimensions is true
        /// </summary>
        [Test]
        public void GetCropUrl_PreDefinedCropNoCoordinatesWithWidthAndFocalPointIgnore()
        {
            const string cropperJson = "{\"focalPoint\": {\"left\": 0.4275,\"top\": 0.41},\"src\": \"/media/1005/img_0671.jpg\",\"crops\": [{\"alias\": \"home\",\"width\": 270,\"height\": 161}]}";

            var urlString = MediaPath.GetCropUrl(new TestImageUrlGenerator(), imageCropperValue: cropperJson, cropAlias: "home", width: 200, useCropDimensions: true);
            Assert.AreEqual(MediaPath + "?f=0.41x0.4275&w=270&h=161", urlString);
        }

        /// <summary>
        /// Test to check if width ratio is returned for a predefined crop without coordinates and focal point in centre when a height parameter is passed
        /// </summary>
        [Test]
        public void GetCropUrl_PreDefinedCropNoCoordinatesWithHeight()
        {
            const string cropperJson = "{\"focalPoint\": {\"left\": 0.5,\"top\": 0.5},\"src\": \"/media/1005/img_0671.jpg\",\"crops\": [{\"alias\": \"home\",\"width\": 270,\"height\": 161}]}";

            var urlString = MediaPath.GetCropUrl(new TestImageUrlGenerator(), imageCropperValue: cropperJson, cropAlias: "home", height: 200);
            Assert.AreEqual(MediaPath + "?m=defaultcrop&wr=1.6770186335403726708074534161&h=200", urlString);
        }

        /// <summary>
        /// Test to check result when only a width parameter is passed, effectivly a resize only
        /// </summary>
        [Test]
        public void GetCropUrl_WidthOnlyParameter()
        {
            const string cropperJson = "{\"focalPoint\": {\"left\": 0.5,\"top\": 0.5},\"src\": \"/media/1005/img_0671.jpg\",\"crops\": [{\"alias\": \"home\",\"width\": 270,\"height\": 161}]}";

            var urlString = MediaPath.GetCropUrl(new TestImageUrlGenerator(), imageCropperValue: cropperJson, width: 200);
            Assert.AreEqual(MediaPath + "?m=defaultcrop&w=200", urlString);
        }

        /// <summary>
        /// Test to check result when only a height parameter is passed, effectivly a resize only
        /// </summary>
        [Test]
        public void GetCropUrl_HeightOnlyParameter()
        {
            const string cropperJson = "{\"focalPoint\": {\"left\": 0.5,\"top\": 0.5},\"src\": \"/media/1005/img_0671.jpg\",\"crops\": [{\"alias\": \"home\",\"width\": 270,\"height\": 161}]}";

            var urlString = MediaPath.GetCropUrl(new TestImageUrlGenerator(), imageCropperValue: cropperJson, height: 200);
            Assert.AreEqual(MediaPath + "?m=defaultcrop&h=200", urlString);
        }

        /// <summary>
        /// Test to check result when using a background color with padding
        /// </summary>
        [Test]
        public void GetCropUrl_BackgroundColorParameter()
        {
            var cropperJson = "{\"focalPoint\": {\"left\": 0.5,\"top\": 0.5},\"src\": \"" + MediaPath + "\",\"crops\": [{\"alias\": \"home\",\"width\": 270,\"height\": 161}]}";

            var urlString = MediaPath.GetCropUrl(new TestImageUrlGenerator(), 400, 400, cropperJson, imageCropMode: ImageCropMode.Pad, furtherOptions: "&bgcolor=fff");
            Assert.AreEqual(MediaPath + "?m=pad&w=400&h=400&bgcolor=fff", urlString);
        }

        internal class TestImageUrlGenerator : IImageUrlGenerator
        {
            public string GetImageUrl(ImageUrlGenerationOptions options)
            {
                var imageProcessorUrl = new StringBuilder(options.ImageUrl ?? string.Empty);

                if (options.FocalPoint != null)
                {
                    imageProcessorUrl.Append("?f=");
                    imageProcessorUrl.Append(options.FocalPoint.Top.ToString(CultureInfo.InvariantCulture));
                    imageProcessorUrl.Append("x");
                    imageProcessorUrl.Append(options.FocalPoint.Left.ToString(CultureInfo.InvariantCulture));
                }
                else if (options.Crop != null)
                {
                    imageProcessorUrl.Append("?c=");
                    imageProcessorUrl.Append(options.Crop.X1.ToString(CultureInfo.InvariantCulture)).Append(",");
                    imageProcessorUrl.Append(options.Crop.Y1.ToString(CultureInfo.InvariantCulture)).Append(",");
                    imageProcessorUrl.Append(options.Crop.X2.ToString(CultureInfo.InvariantCulture)).Append(",");
                    imageProcessorUrl.Append(options.Crop.Y2.ToString(CultureInfo.InvariantCulture));
                }
                else if (options.DefaultCrop)
                {
                    imageProcessorUrl.Append("?m=defaultcrop");
                }
                else
                {
                    imageProcessorUrl.Append("?m=" + options.ImageCropMode.ToString().ToLower());
                    if (options.ImageCropAnchor != null)imageProcessorUrl.Append("&a=" + options.ImageCropAnchor.ToString().ToLower());
                }

                var hasFormat = options.FurtherOptions != null && options.FurtherOptions.InvariantContains("&f=");
                if (options.Quality != null && hasFormat == false) imageProcessorUrl.Append("&q=" + options.Quality);
                if (options.HeightRatio != null) imageProcessorUrl.Append("&hr=" + options.HeightRatio.Value.ToString(CultureInfo.InvariantCulture));
                if (options.WidthRatio != null) imageProcessorUrl.Append("&wr=" + options.WidthRatio.Value.ToString(CultureInfo.InvariantCulture));
                if (options.Width != null) imageProcessorUrl.Append("&w=" + options.Width);
                if (options.Height != null) imageProcessorUrl.Append("&h=" + options.Height);
                if (options.UpScale == false) imageProcessorUrl.Append("&u=no");
                if (options.AnimationProcessMode != null) imageProcessorUrl.Append("&apm=" + options.AnimationProcessMode);
                if (options.FurtherOptions != null) imageProcessorUrl.Append(options.FurtherOptions);
                if (options.Quality != null && hasFormat) imageProcessorUrl.Append("&q=" + options.Quality);
                if (options.CacheBusterValue != null) imageProcessorUrl.Append("&r=").Append(options.CacheBusterValue);

                return imageProcessorUrl.ToString();
            }
        }
    }
}
