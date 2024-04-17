using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using FastReport.Utils;
using FastReport.Preview;
using System.Drawing;
using System.Linq;

namespace FastReport.Export
{
    /// <summary>
    /// The base class for all export filters.
    /// </summary>
    public partial class ExportBase : Base
    {
        private PageRange pageRange;
        private string pageNumbers;
        private int curPage;
        private Stream stream;
        private string fileName;
        private List<int> pages;
        private bool openAfter;
        private bool allowOpenAfter;
        private float zoom;
        private List<string> generatedFiles;
        private bool allowSaveSettings;
        private bool showProgress;
        private int exportTickCount;
        private bool hasMultipleFiles;
        private bool shiftNonExportable;
        private string saveInitialDirectory;
        private List<FileStream> tempFiles;
        private List<Stream> generatedStreams;
        private bool exportTabs;
        protected bool webPreview;

        #region Properties


        /// <summary>
        /// Gets list of generated streams.
        /// </summary>
        public List<Stream> GeneratedStreams
        {
            get { return generatedStreams; }
            protected set { generatedStreams = value; }
        }

        /// <summary>
        /// Gets list of temp files generated by export.
        /// </summary>
        internal List<FileStream> TempFiles
        {
            get { return tempFiles; }
            set { tempFiles = value; }
        }

        /// <summary>
        /// Zoom factor for output file
        /// </summary>
        public float Zoom
        {
            get { return zoom; }
            set { zoom = value; }
        }

        /// <summary>
        /// File filter that can be used in the "Save file" dialog.
        /// </summary>
        public string FileFilter
        {
            get { return GetFileFilter(); }
        }

        /// <summary>
        /// Range of pages to export.
        /// </summary>
        public PageRange PageRange
        {
            get { return pageRange; }
            set { pageRange = value; }
        }

        /// <summary>
        /// Page numbers to export.
        /// </summary>
        /// <remarks>
        /// Use page numbers separated by comma and/or page ranges, for example: "1,3-5,12". Empty string means 
        /// that all pages need to be exported.
        /// </remarks>
        public string PageNumbers
        {
            get { return pageNumbers; }
            set { pageNumbers = value; }
        }

        /// <summary>
        /// Current page number.
        /// </summary>
        /// <remarks>
        /// Page number need to be exported if user selects "Current page" radiobutton in the export options dialog.
        /// This property is typically set to current page number in the preview window.
        /// </remarks>
        public int CurPage
        {
            get { return curPage; }
            set { curPage = value; }
        }

        /// <summary>
        /// Open the document after export.
        /// </summary>
        public bool OpenAfterExport
        {
            get { return openAfter; }
            set { openAfter = value; }
        }

        /// <summary>
        /// Allows or disables the OpenAfterExport feature.
        /// </summary>
        public bool AllowOpenAfter
        {
            get { return allowOpenAfter; }
            set { allowOpenAfter = value; }
        }

        /// <summary>
        /// Gets or sets a value that determines whether to show progress window during export or not.
        /// </summary>
        public bool ShowProgress
        {
            get { return showProgress; }
            set { showProgress = value; }
        }

        /// <summary>
        /// Gets a list of files generated by this export.
        /// </summary>
        public List<string> GeneratedFiles
        {
            get { return generatedFiles; }
        }

        /// <summary>
        /// Gets a value indicating that the export may produce multiple output files.
        /// </summary>
        public bool HasMultipleFiles
        {
            get { return hasMultipleFiles; }
            set { hasMultipleFiles = value; }
        }

        /// <summary>
        /// Gets or sets a value indicating that the report bands should be shifted, if page 
        /// has any non-exportable bands
        /// </summary>
        public bool ShiftNonExportable
        {
            get { return shiftNonExportable; }
            set { shiftNonExportable = value; }
        }

        /// <summary>
        /// Gets or sets the initial directory that is displayed by a save file dialog.
        /// </summary>
        public string SaveInitialDirectory
        {
            get { return saveInitialDirectory; }
            set { saveInitialDirectory = value; }
        }

        /// <summary>
        /// Gets or sets a value indicating that pages will exporting from all open tabs.
        /// </summary>
        public bool ExportAllTabs
        {
            get { return exportTabs; }
            set { exportTabs = value; }
        }

        /// <summary>
        /// Stream to export to.
        /// </summary>
        protected Stream Stream
        {
            get { return stream; }
        }

        /// <summary>
        /// File name to export to.
        /// </summary>
        protected string FileName
        {
            get { return fileName; }
        }

        /// <summary>
        /// Array of page numbers to export.
        /// </summary>
        protected int[] Pages
        {
            get { return pages.ToArray(); }
        }

        internal bool AllowSaveSettings
        {
            get { return allowSaveSettings; }
            set { allowSaveSettings = value; }
        }
        #endregion

        #region Private Methods
        private bool Parse(string pageNumbers, int total)
        {
            pages.Clear();
            string s = pageNumbers.Replace(" ", "");
            if (s == "") return false;

            if (s[s.Length - 1] == '-')
                s += total.ToString();
            s += ',';

            int i = 0;
            int j = 0;
            int n1 = 0;
            int n2 = 0;
            bool isRange = false;

            while (i < s.Length)
            {
                if (s[i] == ',')
                {
                    n2 = int.Parse(s.Substring(j, i - j));
                    j = i + 1;
                    if (isRange)
                    {
                        while (n1 <= n2)
                        {
                            pages.Add(n1 - 1);
                            n1++;
                        }
                    }
                    else
                        pages.Add(n2 - 1);
                    isRange = false;
                }
                else if (s[i] == '-')
                {
                    isRange = true;
                    n1 = int.Parse(s.Substring(j, i - j));
                    j = i + 1;
                }
                i++;
            }

            return true;
        }

        private void PreparePageNumbers()
        {
            pages.Clear();
            int total = Report.PreparedPages.Count;
            if (PageRange == PageRange.Current)
                pages.Add(CurPage - 1);
            else if (!Parse(PageNumbers, total))
            {
                for (int i = 0; i < total; i++)
                    pages.Add(i);
            }

            // remove invalid page numbers
            for (int i = 0; i < pages.Count; i++)
            {
                if (pages[i] < 0 || pages[i] >= total)
                {
                    pages.RemoveAt(i);
                    i--;
                }
            }
        }

        private void OpenFile()
        {
            try
            {
                Process proc = new Process();
                proc.EnableRaisingEvents = false;

#if (NETCOREAPP && !AVALONIA)
                proc.StartInfo.FileName = "cmd";
                proc.StartInfo.Arguments = $"/c \"{fileName}\"";
                proc.StartInfo.CreateNoWindow = true;
#else
                proc.StartInfo = new ProcessStartInfo(fileName) { UseShellExecute = true };
#endif
                proc.Start();
            }
            catch
            {
            }
        }

        #endregion

        #region Protected Methods
        /// <summary>
        /// Returns a file filter for a save dialog.
        /// </summary>
        /// <returns>String that contains a file filter, for example: "Bitmap image (*.bmp)|*.bmp"</returns>
        protected virtual string GetFileFilter()
        {
            return "";
        }

        /// <summary>
        /// This method is called when the export starts.
        /// </summary>
        protected virtual void Start()
        {
            this.Report.OnExportParameters(new ExportParametersEventArgs(this));
        }

        /// <summary>
        /// This method is called at the start of exports of each page.
        /// </summary>
        /// <param name="page">Page for export may be empty in this method.</param>
        protected virtual void ExportPageBegin(ReportPage page)
        {
            page = GetOverlayPage(page);
        }

        /// <summary>
        /// This method is called at the end of exports of each page.
        /// </summary>
        /// <param name="page">Page for export may be empty in this method.</param>
        protected virtual void ExportPageEnd(ReportPage page)
        {
        }

        /// <summary>
        /// This method is called for each band on exported page.
        /// </summary>
        /// <param name="band">Band, dispose after method compite.</param>
        protected virtual void ExportBand(BandBase band)
        {
            band.UpdateWidth();
        }

        /// <summary>
        /// This method is called when the export is finished.
        /// </summary>
        protected virtual void Finish()
        {
        }

        /// <summary>
        /// Gets a report page with specified index.
        /// </summary>
        /// <param name="index">Zero-based index of page.</param>
        /// <returns>The prepared report page.</returns>
        protected ReportPage GetPage(int index)
        {
            ReportPage page = Report.PreparedPages.GetPage(index);
            return GetOverlayPage(page);
        }
        #endregion

        #region Public Methods
        /// <inheritdoc/>
        public override void Assign(Base source)
        {
            BaseAssign(source);
        }

        /// <inheritdoc/>
        public override void Serialize(FRWriter writer)
        {
            writer.WriteValue("PageRange", PageRange);
            writer.WriteStr("PageNumbers", PageNumbers);
            writer.WriteBool("OpenAfterExport", OpenAfterExport);
            writer.WriteBool("ExportAllTabs", ExportAllTabs);
        }

        /// <summary>
        /// Exports the report to a stream.
        /// </summary>
        /// <param name="report">Report to export.</param>
        /// <param name="stream">Stream to export to.</param>
        /// <remarks>
        /// This method does not show an export options dialog. If you want to show it, call <see cref="ShowDialog"/>
        /// method prior to calling this method, or use the "Export(Report report)" method instead.
        /// </remarks>
        public void Export(Report report, Stream stream)
        {
            if (report == null || report.PreparedPages == null)
                return;

            SetReport(report);
            this.stream = stream;
            PreparePageNumbers();
            GeneratedFiles.Clear();
            exportTickCount = Environment.TickCount;

            if (pages.Count > 0)
            {
                if (!String.IsNullOrEmpty(FileName))
                    GeneratedFiles.Add(FileName);
                Start();
                report.SetOperation(ReportOperation.Exporting);

                if (ShowProgress)
                    Config.ReportSettings.OnStartProgress(Report);
                else
                    Report.SetAborted(false);

                try
                {
                    for (int i = 0; i < pages.Count; i++)
                    {
                        if (ShowProgress)
                        {
                            Config.ReportSettings.OnProgress(Report,
                              String.Format(Res.Get("Messages,ExportingPage"), i + 1, pages.Count), i + 1, pages.Count);
                        }
                        if (!Report.Aborted && i < pages.Count)
                            ExportPageNew(pages[i]);
                        else
                            break;
                    }
                }
                finally
                {
                    Finish();
                    DeleteTempFiles();

                    if (ShowProgress)
                        Config.ReportSettings.OnProgress(Report, String.Empty);
                    if (ShowProgress)
                        Config.ReportSettings.OnFinishProgress(Report);

                    report.SetOperation(ReportOperation.None);

                    exportTickCount = Environment.TickCount - exportTickCount;

                    ShowPerformance(exportTickCount);

                    if (openAfter && AllowOpenAfter && stream != null)
                    {
                        stream.Close();
                        OpenFile();
                    }
                }
            }
        }

        public void InstantExportStart(Report report, Stream stream)
        {
            SetReport(report);
            this.stream = stream;
            GeneratedFiles.Clear();
            if (!String.IsNullOrEmpty(FileName))
                GeneratedFiles.Add(FileName);
            Start();
        }

        public void InstantExportBeginPage(ReportPage page)
        {
            ExportPageBegin(page);
        }

        public void InstantExportExportBand(BandBase band)
        {
            ExportBand(band);
        }

        public void InstantExportEndPage(ReportPage page)
        {
            ExportPageEnd(page);
        }

        public void InstantExportFinish()
        {
            Finish();
            DeleteTempFiles();
        }

        /// <summary>
        /// This file will be closed and deleted after the export is finished.
        /// </summary>
        /// <returns></returns>
        internal FileStream CreateTempFile()
        {
            string dir = Path.Combine(Config.GetTempFolder(), "TempExport");
            if (!Directory.Exists(dir))
                Directory.CreateDirectory(dir);

            TempFiles.Add(new FileStream(Path.Combine(dir, Path.GetRandomFileName()), FileMode.CreateNew));
            return TempFiles.Last();
        }

        private void DeleteTempFiles()
        {
            AppDomain.CurrentDomain.UnhandledException -= CurrentDomain_UnhandledException;
            foreach (var file in TempFiles)
            {
                if(file.CanWrite)
                    file.Close();
                File.Delete(file.Name);
            }
            GC.Collect(2);
        }

        private void CurrentDomain_UnhandledException(object sender, EventArgs e)
        {
            DeleteTempFiles();
        }

        internal void ExportPageNew(int pageNo)
        {
            PreparedPage ppage = Report.PreparedPages.GetPreparedPage(pageNo);
            {
                ReportPage page = null;
                try
                {
                    page = ppage.StartGetPage(pageNo);
                    page.Width = ppage.PageSize.Width;
                    page.Height = ppage.PageSize.Height;
                    if (page.Bands.Count == 1 && page.AllObjects.Count == 1)
                        return;
                    else ExportPageBegin(page);
                    float topShift = 0;
                    foreach (Base obj in ppage.GetPageItems(page, false))
                    {
                        BandBase band = obj as BandBase;
                        if (shiftNonExportable && topShift != 0 && obj is BandBase &&
                          !(obj is PageFooterBand) && !band.PrintOnBottom)
                        {
                            band.Top -= topShift;
                        }
                        if (band.Exportable
                            || webPreview)
                            ExportBand(band);
                        else if (obj != null)
                        {
                            if (shiftNonExportable)
                                topShift += band.Height;
                            obj.Dispose();
                        }

                    }
                    ExportPageEnd(page);
                }
                catch (Exception e)
                {
                    Console.WriteLine(e.ToString());
                }
                finally
                {
                    ppage.EndGetPage(page);
                }
                if (page != null)
                    page.Dispose();
            }
        }

        /// <summary>
        /// Exports the report to a file.
        /// </summary>
        /// <param name="report">Report to export.</param>
        /// <param name="fileName">File name to export to.</param>
        /// <remarks>
        /// This method does not show an export options dialog. If you want to show it, call <see cref="ShowDialog"/>
        /// method prior to calling this method, or use the "Export(Report report)" method instead.
        /// </remarks>
        public void Export(Report report, string fileName)
        {
            this.fileName = fileName;
            using (FileStream stream = new FileStream(fileName, FileMode.Create))
            {
                Export(report, stream);
                stream.Close();
            }
        }

        internal string GetFileName(Report report)
        {
            return Path.GetFileNameWithoutExtension(Path.GetFileName(report.FileName));
        }

        internal string GetFileExtension()
        {
            string extension = FileFilter;
            return extension.Substring(extension.LastIndexOf('.'));
        }

        internal void ExportAndZip(Report report, Stream stream)
        {
            string tempFolder = Config.GetTempFolder() + Path.GetRandomFileName();
            Directory.CreateDirectory(tempFolder);
            try
            {
                string filePath = Path.Combine(tempFolder, GetFileName(report) + GetFileExtension());
                Export(report, filePath);
                ZipArchive zip = new ZipArchive();
                zip.AddDir(tempFolder);
                zip.SaveToStream(stream);
            }
            finally
            {
                Directory.Delete(tempFolder, true);
            }
        }
        #endregion

        /// <summary>
        /// Initializes a new instance of the <see cref="ExportBase"/> class.
        /// </summary>
        public ExportBase()
        {
            TempFiles = new List<FileStream>();
            pageNumbers = "";
            pages = new List<int>();
            curPage = 1;
            fileName = "";
            allowOpenAfter = true;
            zoom = 1;
            generatedFiles = new List<string>();
            exportTabs = false;
            shiftNonExportable = false;
            AppDomain.CurrentDomain.UnhandledException += CurrentDomain_UnhandledException;
        }
    }
}
