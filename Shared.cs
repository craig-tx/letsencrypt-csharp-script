public class Shared
{
    public static string GetScriptPath(string[] args)
    {
        return Path.GetDirectoryName(args[0]);
    }

    public static string GetScriptEXE()
    {
        //DirectoryInfo dir = new DirectoryInfo();
        return System.Reflection.Assembly.GetEntryAssembly().Location; //Path.Combine(dir.FullName, "C#.exe");
    }

    public static string GetEXEPath()
    {
        DirectoryInfo dir = new DirectoryInfo(System.Reflection.Assembly.GetEntryAssembly().Location);
        return dir.Parent.FullName;
    }

    public static string GetVersionSingleNum(string[] args)
    {
        return Regex.Split(GetVersionString(args), "\\.")[2];
    }
    public static string GetVersionString(string[] args)
    {
        string[] lines = File.ReadAllLines(GetVersionFilePath(args));
        Console.WriteLine("Version is " + lines[0]);
        return lines[0];
    }
    public static string GetVersionFilePath(string[] args)
    {
        return string.Format(@"{0}\..\..\Build\version.txt", Shared.GetScriptPath(args));
    }

}

public class ProcessStart
{
    /// <summary>
    /// When calling LaunchAndCaptureOutput and you expect the process to take awhile, use this Constant
    /// An example would be applying the Demo Customer Specific 1111 sql file which takes quite a bit of time to run
    /// Time is in Seconds. 900 seconds is 15 minutes
    /// </summary>
    public const int LONG_TIMEOUT = 900;
    /// <summary>
    /// When calling LaunchAndCaptureOutput and you expect the process to run rather fast (about 1 minute), use this Constant
    /// An example would be running a thin provision for customer 0000
    /// Time is in Seconds. 60 seconds is 1 minute
    /// </summary>
    public const int SHORT_TIMEOUT = 60;

    /// <summary>
    /// 2 hours, example robocopy of Attachments
    /// </summary>
    public const int UBER_LONG_TIMEOUT = 2 * 60 * 60; 

    public static bool LaunchAndCaptureOutput(string sPathToExe, string sArgs, int nTimeoutSeconds, out int nExitCode, out string cmdOuput, bool ignoreErrors = false)
    {
        bool bSuccess = false;
        string tempOutputPath = string.Empty;
        string tempBatchPath = string.Empty;
        cmdOuput = string.Empty;
        nExitCode = 0;

        try
        {
            tempOutputPath = Path.GetTempFileName();
            string sTempOutputName = Path.GetFileName(tempOutputPath);
            tempBatchPath = Path.GetTempFileName();
            string sTempBatchName = Path.GetFileNameWithoutExtension(tempBatchPath) + ".cmd";
            tempBatchPath = Path.Combine(Path.GetDirectoryName(tempBatchPath), sTempBatchName);

            string cmdBatch = string.Format("\"{0}\" {1} > {2} 2<&1", sPathToExe, sArgs, sTempOutputName);
            using (StreamWriter sw = new StreamWriter(tempBatchPath))
            {
                sw.WriteLine(cmdBatch.Replace("%", "%%")); // must escape percent chars for batch execution
            }

            string batchArgs = string.Format("/c \"{0}\"", sTempBatchName);
            Console.WriteLine(string.Format("{0} {1}", sPathToExe, sArgs));
            //Console.WriteLine("Batch is: " + batchArgs);

            ProcessStartInfo psi = new ProcessStartInfo("cmd.exe", batchArgs);
            psi.UseShellExecute = true;
            //psi.CreateNoWindow = true;

            psi.WorkingDirectory = Path.GetTempPath();
            psi.WindowStyle = ProcessWindowStyle.Hidden;

            Process pCmd = new Process();
            pCmd.StartInfo = psi;
            DateTime dtStartCmd = DateTime.Now;
            pCmd.Start();

            int counter = 0;
            const int SLEEP_MS = 100;
            const int LOG_EVERY_SECS = 5;
            while (true)
            {
                Thread.Sleep(SLEEP_MS); // Do not set this to a high # such as 5,000 because it really slows down the refresh which spawns many processes.
                if (pCmd.HasExited)
                {
                    break;
                }
                TimeSpan tsElapsed = DateTime.Now.Subtract(dtStartCmd);
                if (counter++ > 4)
                {
                    string theChar = "-";
                    if (counter % 2 == 0) theChar = "/";
                    if (counter % 4 == 0) theChar = "\\";
                    //if (bLogWaiting)
                    {
                        if (counter % (1000 / SLEEP_MS * LOG_EVERY_SECS) == 0) // every few seconds update the UI, in this case 5 seconds
                        {
                            Console.Write("\rWaiting for command to complete {0} ms: " + tsElapsed.TotalMilliseconds.ToString("N0") + "       " + DateTime.Now.ToShortTimeString(), theChar);
                        }
                    }
                }
                if (tsElapsed.TotalSeconds > nTimeoutSeconds)
                {
                    Console.WriteLine("\r\nERROR - Timed out waiting for command to complete - start time " + dtStartCmd.ToString());
                    KillProcessAndChildren(pCmd.Id);
                    Thread.Sleep(3000);
                    break;
                }
            }
            if (counter++ > 20) Console.WriteLine("\r\n");

            if (pCmd.HasExited)
            {
                nExitCode = pCmd.ExitCode;
                Console.WriteLine("Exit code is {0} for {1} ==> {2}", pCmd.ExitCode.ToString(), sPathToExe, sArgs);
                if (ignoreErrors)
                {
                    bSuccess = true; // tell caller it worked, let them check error code
                }
                else
                {
                    bSuccess = (pCmd.ExitCode == 0); // assume non-zero means something failed
                }
                cmdOuput = ReadTempOutputFile(tempOutputPath);
                //Console.WriteLine("Command output is:\r\n\r\n" + sCmdOutput);
            }
            else
            {
                //Console.WriteLine("The process has not exited yet.");
                cmdOuput = string.Format("The process timed out and the output was not read. {0}  {1}", sPathToExe, sArgs);
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine("Exception occurred: {0}", ex);
        }
        finally
        {
            //Console.WriteLine("{0}   {1}", sTempBatch, sTempOutput);
        }
        if (nExitCode == 0)
        {
            //Console.WriteLine("Deleting temp files {0} and {1}", tempBatchPath, tempOutputPath);
            SafeDeleteFile(tempOutputPath);
            SafeDeleteFile(tempBatchPath);
        }
        else
        {
            if (!ignoreErrors)
            {
                Console.WriteLine("\r\n\r\n====BEGIN ERROR TEXT====\r\n\r\n{0}\r\n====END ERROR TEXT====\r\n\r\n", cmdOuput);
            }
            //Console.WriteLine("Not deleting temp files");
        }
        return bSuccess;
    }

    /// <summary>
    /// Kill a process, and all of its children.
    /// http://stackoverflow.com/questions/5901679/kill-process-tree-programatically-in-c-sharp
    /// </summary>
    /// <param name="pid">Process ID.</param>
    private static void KillProcessAndChildren(int pid)
    {
        Console.WriteLine(string.Format("Killing child processes of {0}", pid));
        ManagementObjectSearcher searcher = new ManagementObjectSearcher("Select * From Win32_Process Where ParentProcessID=" + pid);
        ManagementObjectCollection moc = searcher.Get();
        foreach (ManagementObject mo in moc)
        {
            KillProcessAndChildren(Convert.ToInt32(mo["ProcessID"]));
        }
        try
        {
            Process proc = Process.GetProcessById(pid);
            Console.WriteLine(string.Format("Killing process {0}", proc.Id));
            proc.Kill();
        }
        catch (ArgumentException)
        {
            // Process already exited.
        }
    }
    private static string ReadTempOutputFile(string sTempOutputFile)
    {
        try
        {
            string sOutput;
            using (StreamReader strmOutput = new StreamReader(sTempOutputFile))
            {
                sOutput = strmOutput.ReadToEnd();
            }
            return sOutput;
        }
        catch (Exception ex)
        {
            Console.WriteLine("Exception occurred  ReadTempOutputFile: " + ex);
        }
        return string.Empty;
    }
    public static void SafeDeleteFile(string sPath)
    {
        try
        {
            if (File.Exists(sPath))
            {
                File.Delete(sPath);
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine("Exception occurred SafeDeleteFile: {0}", ex);
        }
    }
}

public class ReplacementPair
{
    public string TargetString { get; set; }
    public string ReplacementString { get; set; }
}

