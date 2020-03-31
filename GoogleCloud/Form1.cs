using Google.Apis.Auth.OAuth2;
using Google.Cloud.Storage.V1;
using System;
using System.Collections.Generic;
using System.IO;
using System.Windows.Forms;
using MimeMapping;
using Google.Apis.Upload;
using System.Threading;
using Google.Apis.Download;
using System.Threading.Tasks;
using System.Linq;
using OfficeOpenXml;
using OfficeOpenXml.Table;
using System.Data;
using System.Web.Script.Serialization;

namespace GoogleCloud
{
    public partial class Form1 : Form
    {
        GoogleCredential credential = null;
        string bucketName;
        StorageClient storageClient;
        public Form1()
        {
            InitializeComponent();
            using (var jsonStream = new FileStream("shoponline-229904-44a6448082aa.json", FileMode.Open,
                    FileAccess.Read, FileShare.Read))
            {
                credential = GoogleCredential.FromStream(jsonStream);
            }
            bucketName = "upload-image-product-test";
            storageClient = StorageClient.Create(credential);
            DataGridViewCheckBoxColumn CheckboxColumn = new DataGridViewCheckBoxColumn();
            CheckboxColumn.TrueValue = true;
            CheckboxColumn.FalseValue = false;
            CheckboxColumn.HeaderText = "  Selected  ";
            CheckboxColumn.Width = 100;
            CheckboxColumn.DisplayIndex = 0;
            dataGridView1.Columns.Add(CheckboxColumn);
        }
        private async void button1_Click(object sender, EventArgs e)
        {
            var dlg = new OpenFileDialog();
            if (dlg.ShowDialog() == DialogResult.OK)
            {
                using (var fileStream = new FileStream(dlg.FileName, FileMode.Open,
                    FileAccess.Read, FileShare.Read))
                {
                    progressBar1.Maximum = (int)fileStream.Length;

                    var uploadObjectOptions = new UploadObjectOptions
                    {
                        ChunkSize = UploadObjectOptions.MinimumChunkSize
                    };
                    var progressReporter = new Progress<IUploadProgress>(OnUploadProgress);
                    await storageClient.UploadObjectAsync(bucketName, Path.GetFileName(dlg.FileName), MimeUtility.GetMimeMapping(dlg.FileName), fileStream, uploadObjectOptions, progress: progressReporter).ConfigureAwait(true);
                    btn_getFiles_Click(sender, e);
                }
            }
        }

        // Called when progress updates
        void OnUploadProgress(Google.Apis.Upload.IUploadProgress progress)
        {
            switch (progress.Status)
            {
                case Google.Apis.Upload.UploadStatus.Starting:
                    progressBar1.Minimum = 0;
                    progressBar1.Value = 0;

                    break;
                case Google.Apis.Upload.UploadStatus.Completed:
                    progressBar1.Value = progressBar1.Maximum;
                    break;
                case Google.Apis.Upload.UploadStatus.Uploading:
                    UpdateProgressBar(progress.BytesSent);

                    break;
                case Google.Apis.Upload.UploadStatus.Failed:
                    MessageBox.Show("Upload failed"
                                                   + Environment.NewLine
                                                   + progress.Exception);
                    break;
            }
        }

        void UpdateProgressBar(long value)
        {
            progressBar1.BeginInvoke(new Action(() =>
            {
                progressBar1.Value = (int)value > progressBar1.Maximum ? progressBar1.Maximum : (int)value;
            }));
        }

        public string BytesToReadableValue(long number)
        {
            var suffixes = new List<string> { " B", " KB", " MB", " GB", " TB", " PB" };

            for (int i = 0; i < suffixes.Count; i++)
            {
                long temp = number / (int)Math.Pow(1024, i + 1);

                if (temp == 0)
                {
                    return (number / (int)Math.Pow(1024, i)) + suffixes[i];
                }
            }

            return number.ToString();
        }

        private void btn_getFiles_Click(object sender, EventArgs e)
        {
            var files = new List<fileInfo>();
            foreach (var obj in storageClient.ListObjects(bucketName, ""))
            {
                var file = new fileInfo();
                file.id = obj.Generation.ToString();
                file.md5 = obj.Md5Hash;
                file.name = obj.Name;
                file.size = obj.Size + "";
                file.sizeText = BytesToReadableValue(long.Parse(obj.Size.ToString()));
                file.link = obj.MediaLink;
                file.SelfLink = $"http://storage.googleapis.com/{bucketName}/" + obj.Name;

                files.Add(file);
            }
            dataGridView1.DataSource = files;
            lbl_file.DataBindings.Clear();
            lbl_file.DataBindings.Add("text", files, "name");
            lbl_byte.DataBindings.Clear();
            lbl_byte.DataBindings.Add("text", files, "size");
        }

        private async void btn_download_Click(object sender, EventArgs e)
        {
            var dlg = new SaveFileDialog();
            dlg.FileName = lbl_file.Text;
            if (dlg.ShowDialog() == DialogResult.OK)
            {
                var token = new CancellationTokenSource().Token;

                using (var fileStream = File.Create(dlg.FileName))
                {
                    progressBar1.Maximum = int.Parse(lbl_byte.Text);

                    var downloadObjectOptions = new DownloadObjectOptions
                    {
                        ChunkSize = UploadObjectOptions.MinimumChunkSize
                    };
                    var progressReporter = new Progress<IDownloadProgress>(OnDownloadProgress);

                    await storageClient.DownloadObjectAsync(bucketName, Path.GetFileName(dlg.FileName), fileStream, downloadObjectOptions, token, progress: progressReporter).ConfigureAwait(true);

                    //storageClient.do
                }
            }
        }

        void OnDownloadProgress(IDownloadProgress progress)
        {
            switch (progress.Status)
            {
                case DownloadStatus.Completed:
                    progressBar1.Value = progressBar1.Maximum;
                    break;
                case DownloadStatus.Downloading:
                    UpdateProgressBar(progress.BytesDownloaded);

                    break;
                case DownloadStatus.Failed:
                    MessageBox.Show("Download failed"
                                                   + Environment.NewLine
                                                   + progress.Exception);
                    break;
            }
        }

        private void btn_delete_Click(object sender, EventArgs e)
        {
            storageClient.DeleteObject(bucketName, lbl_file.Text);
            btn_getFiles_Click(sender, e);
        }

        private async void button2_Click(object sender, EventArgs e)
        {
            if (string.IsNullOrEmpty(textFolder.Text))
            {
                MessageBox.Show("You need to enter the folder address !");
            }
            else
            {
                var results = new List<DataFolder>();
                string[] subDirs = Directory.GetDirectories(textFolder.Text);
                var taskUploadImageVarions = new List<Task<DataFolder>>();
                foreach (var sub in subDirs)
                {
                    var extension = Path.GetExtension(sub);
                    string directionTo = "";
                    if (!string.IsNullOrEmpty(extension))
                    {
                        directionTo = DeleteExtension(sub, extension);
                        Directory.Move(sub, directionTo);
                    }
                    else
                    {
                        directionTo = sub;
                    }

                    var directories = Directory.GetDirectories(directionTo);
                    var subList = Directory.GetFiles(directionTo);
                    var customImage = new ImageVariation();
                    customImage.RootImage = subList.FirstOrDefault();
                    var subList2 = Directory.GetFiles(directories.FirstOrDefault());
                    customImage.VarionImages.AddRange(subList2);
                    try
                    {
                        var result = await UploadStream(customImage, sender, e);
                        results.Add(result);
                    }
                    catch (Exception ex)
                    {
                        Export(results);
                        break;
                    }
                }
               
                Export(results);
            }
        }

        private async Task<DataFolder> UploadStream(ImageVariation url, object sender, EventArgs e)
        {
            var response = new DataFolder();
            if (url != null)
            {
                foreach (var _url in url.VarionImages)
                {
                    var urlImage = await UploadEachFile(_url, false);
                    response.VariationImages.Add(urlImage);
                }
                var imageRoot = await UploadEachFile(url.RootImage, false);
                response.RootImage = imageRoot;
            }
            return response;
        }

        private async Task<PropertyImage> UploadEachFile(string url, bool isVariation)
        {
            if (!string.IsNullOrEmpty(url))
            {
                var extension = Path.GetExtension(url);
                var filName = GetFileName(url, extension);
                var variation = !isVariation ? "" : GetVariation(url, extension);
                using (var fileStream = new FileStream(url, FileMode.Open,
                   FileAccess.Read, FileShare.Read))
                {
                    progressBar1.Maximum = (int)fileStream.Length;

                    var uploadObjectOptions = new UploadObjectOptions
                    {
                        ChunkSize = UploadObjectOptions.MinimumChunkSize
                    };
                    var progressReporter = new Progress<IUploadProgress>(OnUploadProgress);
                    var objectName = Path.GetFileName(url);
                    var data = await storageClient.UploadObjectAsync(bucketName,
                        objectName,
                        MimeUtility.GetMimeMapping(url),
                        fileStream, uploadObjectOptions,
                        progress: progressReporter
                        ).ConfigureAwait(true);
                    var link = $"http://storage.googleapis.com/{bucketName}/" + data.Name;
                    return new PropertyImage()
                    {
                        FileName = filName,
                        PropertyVariation = variation,
                        Url = link
                    };
                }
            }
            return new PropertyImage();
        }

        public void fnExportTableToExcel(DataTable dt)
        {
            //Hiện hộp thoại chọn đường dẫn lưu file Excel
            using (SaveFileDialog saveDialog = new SaveFileDialog())
            {
                saveDialog.Filter = "Excel (Phiên bản 2007 trở lên (.xlsx)|*.xlsx";
                if (saveDialog.ShowDialog() != DialogResult.Cancel)
                {

                    string exportFilePath = saveDialog.FileName;
                    var newFile = new FileInfo(exportFilePath);
                    ExcelPackage.LicenseContext = LicenseContext.NonCommercial;
                    using (var package = new ExcelPackage(newFile))
                    {
                        //Tạo 1 Sheet mới với tên Sheet là NewSheet1
                        ExcelWorksheet worksheet = package.Workbook.Worksheets.Add("NewSheet1");
                        //Load dữ liệu từ DataTable dt vào WorkSheet vừa tạo, bắt đầu từ ô A1, với kiểu Table không có định dạng
                        worksheet.Cells["A1"].LoadFromDataTable(dt, true, TableStyles.None);
                        //Lưu file Excel
                        package.Save();
                    }
                }
            }
        }

        private void ExportFileSelected(List<PropertyImage> images)
        {
            DataTable dt = new DataTable();
            dt.Columns.Add("name", typeof(String));
            dt.Columns.Add("url", typeof(String));
            dt.AcceptChanges();
            foreach (var image in images)
            {
                DataRow row = dt.NewRow();
                row["name"] = image.FileName;
                row["url"] = image.Url;
                dt.Rows.Add(row);
                dt.AcceptChanges();
            }
            fnExportTableToExcel(dt);
        }
        private void Export(List<DataFolder> data)
        {
            DataTable dt = new DataTable();
            dt.Columns.Add("item_name", typeof(String));
            dt.Columns.Add("variations", typeof(String));
            dt.Columns.Add("main_image_url", typeof(String));
            dt.AcceptChanges();
            foreach (var item in data)
            {
                DataRow row = dt.NewRow();
                row["item_name"] = item.RootImage.FileName;
                row["variations"] = new JavaScriptSerializer().Serialize(item.VariationImages.Select(x => new JsonClass() { color = x.PropertyVariation, main_image_url = x.Url }).ToList());
                row["main_image_url"] = item.RootImage.Url;
                dt.Rows.Add(row);
                dt.AcceptChanges();
            }
            fnExportTableToExcel(dt);
        }

        private string GetFileName(string url, string extension)
        {
            string[] parts = url.Split('\\');
            foreach (var part in parts)
            {
                if (part.Contains(extension))
                {
                    return part;
                }
            }
            return string.Empty;
        }

        private string GetVariation(string fileName, string extension)
        {
            int pTo = fileName.IndexOf(extension);
            int pFrom = fileName.LastIndexOf("-") + 1;
            var result = fileName.Substring(pFrom, pTo - pFrom);
            return result;
        }

        private void dataGridView1_CellContentClick(object sender, DataGridViewCellEventArgs e)
        {

        }

        private void btnExport_Click(object sender, EventArgs e)
        {
            var prepareImages = new List<PropertyImage>();
            foreach (DataGridViewRow row in dataGridView1.Rows)
            {
                DataGridViewCheckBoxCell chk = (DataGridViewCheckBoxCell)row.Cells[0];
                if (chk.Value == chk.TrueValue)
                {
                    var image = (DataGridViewTextBoxCell)row.Cells[3];
                    var url = (DataGridViewTextBoxCell)row.Cells[6];
                    if (image != null)
                    {
                        prepareImages.Add(new PropertyImage()
                        {
                            FileName = (string)image.Value,
                            Url = (string)url.Value
                        });
                    }
                }
            }
            ExportFileSelected(prepareImages);
        }

        private string DeleteExtension(string url, string extension)
        {
            if (string.IsNullOrEmpty(url))
            {
                return string.Empty;
            }
            if (!string.IsNullOrEmpty(extension))
            {
                url.Replace(extension, string.Empty);
            }
            return url;
        }
    }


    public class JsonClass
    {
        public string color { get; set; }
        public string main_image_url { get; set; }
    }
    public class PropertyImage
    {
        public string FileName { get; set; }
        public string Url { get; set; }
        public string PropertyVariation { get; set; }
    }

    public class fileInfo
    {
        public string id { get; set; }
        public string md5 { get; set; }
        public string name { get; set; }
        public string size { get; set; }
        public string sizeText { get; set; }
        public string link { get; set; }
        public string SelfLink { get; set; }
    }

    public class ImageVariation
    {
        public ImageVariation()
        {
            VarionImages = new List<string>();
        }
        public List<string> VarionImages { get; set; }
        public string RootImage { get; set; }
    }

    public class DataFolder
    {
        public DataFolder()
        {
            VariationImages = new List<PropertyImage>();
        }
        public List<PropertyImage> VariationImages { get; set; }
        public PropertyImage RootImage { get; set; }
    }
}
