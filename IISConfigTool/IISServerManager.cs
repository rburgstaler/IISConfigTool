﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Microsoft.Web.Administration;
using System.IO;
using System.Reflection;

namespace IISUtil
{
    public class IISServerManagerSite : IISSite
    {
        //public static bool DeleteSite(IISIdentifier siteIdentifier)
        //{
        //    String id = "";
        //    //need to be sure that the site exists or else it can throw an error
        //    if (IISWMIHelper.TryGetSiteID(siteIdentifier, ref id))
        //    {
        //        DirectoryEntry webServer = IISWMIHelper.GetIIsWebServer(id);
        //        webServer.Invoke("Stop", null);
        //        webServer.DeleteTree();
        //        return true;
        //    }
        //    return false;
        //}

        ServerManager ServerMgr = new ServerManager();
        Site site;
        public static IISServerManagerSite CreateNewSite(String serverComment, String serverBindings, String filePath)
        {
            Directory.CreateDirectory(filePath);
            IISServerManagerSite retVal = new IISServerManagerSite();
            retVal.site = retVal.ServerMgr.Sites.Add(serverComment, filePath, 80);
            retVal.SetBindings(serverBindings);

            //We also need to setup an app pool most likely
            ApplicationPool appPool = retVal.ServerMgr.ApplicationPools[serverComment];
            appPool = appPool ?? retVal.ServerMgr.ApplicationPools.Add(serverComment);
            retVal.site.ApplicationDefaults.ApplicationPoolName = serverComment;

            return retVal;
        }

        ////Return null if the site is not to be found
        //public static IISWMISite FindSite(IISIdentifier Identifier)
        //{
        //    String id = "";
        //    //need to be sure that the site exists or else it can throw an error
        //    if (IISWMIHelper.TryGetSiteID(Identifier, ref id))
        //    {
        //        return new IISWMISite()
        //        {
        //            SiteId = id
        //        };
        //    }
        //    return null;
        //}

        ////http:*:80:www.abcdefg.com
        ////https:*:443:www.abcdefg.com
        public override void SetBindings(String siteBindings)
        {
            site.Bindings.Clear();
            //We need to parse the bindings string
            IISBindingParser.Parse(siteBindings,
                delegate(IISBinding iisBinding)
                {
                    if (iisBinding.Protocol.Equals("http", StringComparison.CurrentCultureIgnoreCase))
                    {
                        Microsoft.Web.Administration.Binding binding = site.Bindings.CreateElement("binding");
                        binding.Protocol = iisBinding.Protocol;
                        binding.BindingInformation = iisBinding.SMBindString;
                        site.Bindings.Add(binding);
                    }
                });
            ServerMgr.CommitChanges();
        }

        public override void Start()
        {
            //Start will report an error "The object identifier does not represent a valid object. (Exception from 
            //HRESULT: 0x800710D8)" if we don't give some time as mentioned by Sergei - http://forums.iis.net/t/1150233.aspx
            //There is a timing issue. WAS needs more time to pick new site or pool and start it, therefore (depending on your system) you could 
            //see this error, it is produced by output routine. Both site and pool are succesfully created, but State field of their PS 
            //representation needs runtime object that wasn't created by WAS yet.
            //He said that would be fixed soon, but apparently that did not take place yet so we will work around it.
            DateTime giveUpAfter = DateTime.Now.AddSeconds(3);
            while (true)
            {
                try
                {
                    site.Start();
                    break;
                }
                catch (Exception exp)
                {
                    if (DateTime.Now > giveUpAfter)
                    {
                        throw new Exception(String.Format("Inner error: {0} Outer error: {1}", (exp.InnerException != null) ? exp.InnerException.Message : "No inner exception", exp.Message));
                        break;
                    }
                }
                System.Threading.Thread.Sleep(250);
            }
        }

        //private T GetVirtualDirPropertyDef<T>(String propertyName, T DefaultValue)
        //{
        //    PropertyValueCollection pc = IISWMIHelper.GetIIsWebVirtualDir(SiteId).Properties["propertyName"];
        //    return (pc != null) ? (T)Convert.ChangeType(pc.Value, typeof(T)) : DefaultValue;
        //}
        //private void SetVirtualDirProperty(String propertyName, object propertyValue)
        //{
        //    DirectoryEntry virDir = IISWMIHelper.GetIIsWebVirtualDir(SiteId);
        //    virDir.Properties[propertyName].Value = propertyValue;
        //    virDir.CommitChanges();
        //}

        public override String DefaultDoc
        {
            get
            {
                return "";  //Not implemented yet
            }
            set
            {
                //Not implemented yet
            }
        }

        public override String AppPoolId
        {
            get
            {
                return site.ApplicationDefaults.ApplicationPoolName;
            }
            set
            {
                site.ApplicationDefaults.ApplicationPoolName = value;
                ServerMgr.CommitChanges();
            }
        }

        public override Int32 AccessFlags
        {
            get
            {
                return 0;
            }
            set
            {
                //Not implemented yet
            }
        }

        public override Int32 AuthFlags
        {
            get
            {
                return 0;
            }
            set
            {
                //Not implemented yet
            }
        }

        public override void SetASPDotNetVersion(AspDotNetVersion version)
        {
            ApplicationPool appPool = ServerMgr.ApplicationPools[site.ApplicationDefaults.ApplicationPoolName];
            appPool.ManagedRuntimeVersion = AspDotNetServerManagerVersionConst.VersionString(version);
            ServerMgr.CommitChanges();
        }
    }


    public static class AspDotNetServerManagerVersionConst
    {
        public const string AspNetV1 = "v1.0";
        public const string AspNetV11 = "v1.0";
        public const string AspNetV2 = "v2.0";
        public const string AspNetV4 = "v4.0";

        public static String VersionString(AspDotNetVersion version)
        {
            FieldInfo fi = typeof(AspDotNetServerManagerVersionConst).GetField(version.ToString());
            return (fi == null) ? "" : Convert.ToString(fi.GetValue(null));
        }
    }
}
