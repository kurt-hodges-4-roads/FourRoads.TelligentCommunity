﻿using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using FourRoads.Common;
using FourRoads.TelligentCommunity.MetaData.Api;
using FourRoads.TelligentCommunity.MetaData.Interfaces;
using Telligent.Evolution.Components;
using Telligent.Evolution.Extensibility.Api.Entities.Version1;

namespace FourRoads.TelligentCommunity.MetaData.ScriptedFragmentss
{
    public class MetaDataScriptedFragment : IMetaDataScriptedFragment
    {
        private IMetaDataLogic _metaDataLogic;

        public MetaDataScriptedFragment(IMetaDataLogic metaDataLogic)
        {
            _metaDataLogic = metaDataLogic;
        }

        protected IMetaDataLogic MetaDataLogic
        {
            get
            {
                return _metaDataLogic;
            }
        }

        public bool CanEdit
        {
            get { return MetaDataLogic.CanEdit; }
        }

        public string[] GetAvailableExtendedMetaTags()
        {
            return MetaDataLogic.GetAvailableExtendedMetaTags();
        }

        public string GetDynamicFormXml()
        {
            return MetaDataLogic.GetDynamicFormXml();
        }

        public string SaveMetaDataConfiguration(string title, string description, string keywords , IDictionary extendedTags )
        {
            try
            {
                MetaDataLogic.SaveMetaDataConfiguration(title, description, keywords, extendedTags);
            }
            catch (Exception ex)
            {
                new CSException("MetaData Plugin" , "Save Failed" , ex).Log();

                return "Save Meta Data failed";
            }

            return string.Empty;
        }

        public ApiMetaData GetCurrentMetaData()
        {
            Logic.MetaData metaData;
            try
            {
                metaData = MetaDataLogic.GetCurrentMetaData();

                if (metaData == null)
                    return null;
            }
            catch (Exception ex)
            {
                return new ApiMetaData(new AdditionalInfo(new Error("Exception", ex.Message)));
            }

            return new ApiMetaData(metaData);
        }
    }
}