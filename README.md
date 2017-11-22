# letsencrypt-csharp-script
A C# script to automate Lets Encrypt certificate renewal for a Windows Apache installation.

This script automates a few steps such as:
 1) Archiving old SSL cert files
 2) Executing the Lets Encrypt program to request new certificate info
 3) Copying the new certificate files to the Apache config folder
 4) Updating the Apache config files to point to the new files
 5) Bouncing the Apache service so the new cert files are in action
 
 To run this just download the files into a folder and execute the script passing the 7 arguments. You could schedule this for each domain when it gets close to cert expiration but you will need to make sure the website can be accessed on port 80 for the renewal process. I have several sites running at home on alternate ports, so I have to tweak my port forwards so each site is temporarily accessible on port 80 while I run the script. For more info on that see: https://community.letsencrypt.org/t/support-for-ports-other-than-80-and-443/3419/100
 
 Arguments:  
 ```
 // 0 path to letsencrypt   - "C:\apps\letsencrypt-win-simple\letsencrypt.exe"  
 // 1 domain to gen SSL For - yourdomain.com  
 // 2 webroot for domain    - C:\apache_apps\wwwroot.80  
 // 3 apache Config file    - "C:\apps\WampServer\bin\apache\Apache2.2.21\conf\extra\httpd-ssl.conf"  
 // 4 do it for real        - TEST | NOT-TEST  
 // 5 service to bounce     - apacheservicename  
 // 6 email for regist      - sslreg@yourdomain.com  
```
# Example:
 ```
C# renew.cs "C:\apps\letsencrypt-win-simple\letsencrypt.exe" test.com C:\apache_apps\wwwroot.80 "C:\apps\WampServer\bin\apache\Apache2.2.21\conf\extra\httpd-ssl.conf" NOT-TEST wampapache    sslreg@test.com
 ```
