#pragma warning disable 1633 // disable unrecognized pragma directive warning
#pragma reference "System.Windows.Forms.dll"
#pragma reference "System.dll"
#pragma reference "System.Core.dll"
#pragma reference "System.Xml.dll"
#pragma reference "System.Xml.Linq.dll"
#pragma reference "mscorlib.dll"
#pragma reference "System.Management.dll"

using System.Windows.Forms;
using System;
using System.IO;
using System.Threading;
using System.Diagnostics;
using System.Collections.Generic;
using System.Net;
using System.Net.Mail;
using System.Net.Sockets;
using System.Reflection;
using System.Text.RegularExpressions;
using System.Management;

public class SSLRenew
{
	// prereq - install letsencrypt-win-simple locally
    // cmd line example
    // pushd C:\apps\SSL-Renew-LetsEncrypt
	//              PATH-TO-LETSENCRYPT                             SITE-HOSTNAME LOCAL_FOLDER_PATH         PATH-TO-SSL-CONFIG                                                     TEST?    SERVICE-NAME    
	// C# renew.cs "C:\apps\letsencrypt-win-simple\letsencrypt.exe" craigliz.com  C:\apache_apps\wwwroot.80 "C:\apps\WampServer\bin\apache\Apache2.2.21\conf\extra\httpd-ssl.conf" NOT-TEST wampapache


	static void Main(string[] args)
    {
		try
		{
			if (args.Length != 6)
			{
				Console.WriteLine("Arguments:" + Environment.NewLine +
								  "// 0 path to letsencrypt   - \"C:\\apps\\letsencrypt-win-simple\\letsencrypt.exe\"" + Environment.NewLine +
								  "// 1 domain to gen SSL For - craigliz.com" + Environment.NewLine +
								  "// 2 webroot for domain    - C:\\apache_apps\\wwwroot.80 " + Environment.NewLine +
								  "// 3 apache Config file    - \"C:\\apps\\WampServer\\bin\\apache\\Apache2.2.21\\conf\\extra\\httpd-ssl.conf\" " + Environment.NewLine +
								  "// 4 do it for real        - TEST | NOT-TEST " + Environment.NewLine +
								  "// 5 service to bounce     - apacheservicename " + Environment.NewLine + 
								  "// 6 email for regist      - sslreg@yourdomain.com" + Environment.NewLine); 
								  
				return;
			}

			string LE_EXE = args[0];
			string LE_DOMAIN = args[1];
			string LE_WEBROOT = args[2];
			string LE_APACHE_CONFIG = args[3];
			string LE_TEST = args[4];
			string SVC_BOUNCE = args[5];
			string EMAIL_REG = args[6];
			string testFlag = (LE_TEST == "NOT-TEST") ? "" : "--test";

			if (!ArgsCheck(LE_EXE, LE_WEBROOT, LE_APACHE_CONFIG)) return;
			
			Console.WriteLine("\r\n\r\n\r\nHave you made sure to set the firewall so website {0} is publicly available on 80?  y/n\r\n\r\n", LE_DOMAIN);
			string reply = Console.ReadLine();
			if (reply != "y") return;
			
			// Test mode get files from diff folder
			string sslWorkFolder = (testFlag == "--test") ? @"letsencrypt-win-simple\httpsacme-staging.api.letsencrypt.org" : @"letsencrypt-win-simple\httpsacme-v01.api.letsencrypt.org";
			string sslOutDir = Path.Combine(Environment.ExpandEnvironmentVariables("%APPDATA%"), sslWorkFolder);
			
			Console.WriteLine(sslOutDir);
			
			// archive existing files
			if (Directory.GetFiles(sslOutDir).Length > 0)
			{
				string dtFolder = DateTime.Now.ToString("yyyy.MM.dd-HH-mm-ss");
				string archiveFolder = Path.Combine(sslOutDir, dtFolder);
				Directory.CreateDirectory(archiveFolder);
				foreach(var file in Directory.GetFiles(sslOutDir))
				{
					string newPath = Path.Combine(Path.GetDirectoryName(file), archiveFolder, Path.GetFileName(file));
					File.Move(file, newPath);  
					Console.WriteLine("ARCHIVED - " + newPath);
				}
			}
			
			string cmdOutput = string.Empty;
			int exitCode = 0;
			string cmdArgs = string.Format("--manualhost {0} --webroot \"{1}\" --emailaddress {2} --signeremail {2} --accepttos {3}", LE_DOMAIN, LE_WEBROOT, EMAIL_REG, testFlag);

			Console.WriteLine("\r\n\r\n\r\nCMD-TO-EXEC ===>  {0} {1}\r\n\r\nLook good?     y/n\r\n\r\n", LE_EXE, cmdArgs);
			reply = Console.ReadLine();
			if (reply != "y") return;
			
			ProcessStart.LaunchAndCaptureOutput(LE_EXE, cmdArgs, 240, out exitCode, out cmdOutput);
			
			if (exitCode == 0)
			{
				string numPEM = "";
				string crtPEM = "";
				string keyPEM = "";
				// find ca-num-crt.pem
				var filesEnum = Directory.GetFiles(sslOutDir);
				foreach (var oFile in filesEnum)
				{
					string fnameOnly = Path.GetFileName(oFile);
					if (fnameOnly.Left(3).ToLower() == "ca-" && fnameOnly.Right(4).ToLower() == ".pem")
					{
						numPEM = oFile;
					}
					else if (fnameOnly.Right(8).ToLower() == "-crt.pem")
					{
						crtPEM = oFile;
					}
					else
					{
						if (fnameOnly.Right(8).ToLower() == "-key.pem")
						{
							keyPEM = oFile;
						}
					}
				}
				Console.WriteLine("NUMPEN:{0}\r\nCRTPEM:{1}\r\nKEYPEM:{2}\r\n", numPEM, crtPEM, keyPEM);
				if (numPEM.Length > 0 && crtPEM.Length > 0 && keyPEM.Length > 0)
				{
					// copy files to apacheCONF folder - new location will be what we ref in config
					string newFolder = Path.GetDirectoryName(LE_APACHE_CONFIG);
					string numPEMNew = Path.Combine(newFolder, Path.GetFileName(numPEM));
					string crtPEMNew = Path.Combine(newFolder, Path.GetFileName(crtPEM));
					string keyPEMNew = Path.Combine(newFolder, Path.GetFileName(keyPEM));
					File.Copy(numPEM, numPEMNew, true);
					File.Copy(crtPEM, crtPEMNew, true);
					File.Copy(keyPEM, keyPEMNew, true);
					
					// Now update ApacheConfig with the new values
					string[] lines = File.ReadAllLines(LE_APACHE_CONFIG);
					string newConfig = "";
					foreach(var ln in lines)
					{
						string lnMod = ln;
						if (ln.Contains("SSLCertificateChainFile")) lnMod = string.Format("SSLCertificateChainFile \"{0}\"", numPEMNew.ToFwdSlash());
						if (ln.Contains("SSLCertificateFile")) lnMod = string.Format("SSLCertificateFile \"{0}\"", crtPEMNew.ToFwdSlash());
						if (ln.Contains("SSLCertificateKeyFile")) lnMod = string.Format("SSLCertificateKeyFile \"{0}\"", keyPEMNew.ToFwdSlash());
						newConfig += string.Format("{0}\r\n", lnMod);
					}
					//Console.WriteLine(newConfig);
					File.WriteAllText(LE_APACHE_CONFIG, newConfig);
				}
				else
				{
					Console.WriteLine("FAILURE - could not find output files");
				}
				
			}
			
			Console.WriteLine("Exit code {0}\r\nOutput:\r\n{1}", exitCode, cmdOutput);
			
			ProcessStart.LaunchAndCaptureOutput("net.exe", "stop " + SVC_BOUNCE, 240, out exitCode, out cmdOutput);
			
			Console.WriteLine("Exit code {0}\r\nOutput:\r\n{1}", exitCode, cmdOutput);
			
			ProcessStart.LaunchAndCaptureOutput("net.exe", "start " + SVC_BOUNCE, 240, out exitCode, out cmdOutput);
			
			Console.WriteLine("Exit code {0}\r\nOutput:\r\n{1}", exitCode, cmdOutput);
			
		}
		catch (Exception ex)
		{
			Console.WriteLine(ex.ToString());
		}
	}
	private static bool ArgsCheck(string exe, string webroot, string cfg)
	{
		if (!File.Exists(exe))
		{
			Console.WriteLine("EXE no exist!!");
			return false;
		}
		if (!Directory.Exists(webroot))
		{
			Console.WriteLine("WEBROOT no exist!!");
			return false;
		}
		if (!File.Exists(cfg))
		{
			Console.WriteLine("CFG no exist!!");
			return false;
		}
		return true;
	}
	
}
public static class StringExtension
{
	public static string ToFwdSlash(this string sValue)
	{
		return sValue.Replace("\\", "/");
	}
	
	public static string Right(this string sValue, int iMaxLength)
	{
	  //Check if the value is valid
	  if (string.IsNullOrEmpty(sValue))
	  {
		//Set valid empty string as string could be null
		sValue = string.Empty;
	  }
	  else if (sValue.Length > iMaxLength)
	  {
		//Make the string no longer than the max length
		sValue = sValue.Substring(sValue.Length - iMaxLength, iMaxLength);
	  }

	  //Return the string
	  return sValue;
	}	
	public static string Left(this string s, int length)
	{
		length = Math.Max(length, 0);
		if (s.Length > length)
		{
			return s.Substring(0, length);
		}
		else
		{
			return s;
		}
	}	
}

//#include shared.cs

