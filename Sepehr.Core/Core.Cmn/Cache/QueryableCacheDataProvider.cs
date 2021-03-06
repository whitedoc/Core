﻿using Core.Serialization;
using System;
using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Data;
using System.Globalization;
using System.Linq;
using System.Linq.Dynamic;
using System.Runtime.Serialization;
using System.ServiceModel;

namespace Core.Cmn.Cache
{
    [DataContract]
    public abstract class CacheDataProvider : ICacheDataProvider
    {
        private static readonly CultureInfo DefaultCultureInfo = new CultureInfo("en-US");
        public static ConcurrentDictionary<int, string[]> TimeStamps;

        static CacheDataProvider()
        {
            TimeStamps = new ConcurrentDictionary<int, string[]>();
        }

        public CacheDataProvider(CacheInfo cacheInfo)
        {
            CacheInfo = cacheInfo;
        }

        [DataMember]
        public CacheInfo CacheInfo { get; set; }

        /// <summary>
        ///
        /// </summary>
        /// <param name="resultList"></param>
        /// <param name="cacheInfo"></param>
        /// <param name="isForServerSide">this is 'true' if must excecute in cache server...</param>
        public static void CalcAllTimeStampAndSet(IEnumerable resultList, CacheInfo cacheInfo, bool isForServerSide)
        {
            var dataLst = resultList.Cast<ObjectBase>().ToList();
            if (dataLst.Count > 0 && cacheInfo.EnableToFetchOnlyChangedDataFromDB)
            {
                CalcTimeStampAndSet(cacheInfo, resultList.Cast<ObjectBase>().ToList(), null, isForServerSide);
                if (!string.IsNullOrWhiteSpace(cacheInfo.NameOfNavigationPropsForFetchingOnlyChangedDataFromDB))
                {
                    var navigationPropsForGettingChangedData = cacheInfo.NameOfNavigationPropsForFetchingOnlyChangedDataFromDB.Split(',');
                    foreach (var prop in navigationPropsForGettingChangedData)
                    {
                        CalcTimeStampAndSet(cacheInfo, resultList.Cast<ObjectBase>().ToList(), prop, isForServerSide);
                    }
                }
            }
        }

        /// <summary>
        ///
        /// </summary>
        /// <param name="cacheInfo"></param>
        /// <param name="resultList"></param>
        /// <param name="key"></param>
        /// <param name="isForServerSide">this is 'true' if must excecute in cache server...</param>
        public static void CalcTimeStampAndSet(CacheInfo cacheInfo, List<ObjectBase> resultList, string key, bool isForServerSide)
        {
            ulong maxTimeStampUnit = 0;
            byte[] maxTimeStamp = null;
            if (key == null)
            {
                CalcTimeStamp(resultList, out maxTimeStampUnit, out maxTimeStamp);

                if (cacheInfo.MaxTimeStampUint < maxTimeStampUnit)
                {
                    cacheInfo.MaxTimeStampUint = maxTimeStampUnit;
                    cacheInfo.MaxTimeStamp = maxTimeStamp;
                }
            }
            else
            {
                IEnumerable<ObjectBase> navData = resultList;
                var WhereClauseForFetchingOnlyChangedDataFromDB = " or ({0} {1}) ";
                var props = string.Empty;
                foreach (var prop in key.Split('.'))
                {
                    if (navData.Count() == 0)
                    {
                        WhereClauseForFetchingOnlyChangedDataFromDB = null;
                        return;
                    }

                    var firstItem = navData.FirstOrDefault(item => item[prop] != null);

                    if (firstItem == null)
                    {
                        WhereClauseForFetchingOnlyChangedDataFromDB = null;
                        return;
                    }

                    var whereClause_CheckingNotNullPart = string.Empty;

                    if ((typeof(IEnumerable)).IsAssignableFrom(firstItem[prop].GetType()))
                    {
                        props = prop;
                        navData = navData.Where((ObjectBase itm) => itm[prop] != null).SelectMany((Func<ObjectBase, IEnumerable<ObjectBase>>)((ObjectBase item) => ((IEnumerable)item[prop]).Cast<ObjectBase>()));
                        //  WhereClauseForFetchingOnlyChangedDataFromDB = string.Format(WhereClauseForFetchingOnlyChangedDataFromDB, props + " != null and ", prop + ".Any( {0}  {1} )");
                        WhereClauseForFetchingOnlyChangedDataFromDB = string.Format(WhereClauseForFetchingOnlyChangedDataFromDB, "", prop + ".Any( {0}  {1} )");

                        props = string.Empty;
                    }
                    else
                    {
                        props += prop;
                        navData = navData.Where((ObjectBase itm) => itm[prop] != null).Select(((ObjectBase item) => (ObjectBase)item[prop]));
                        WhereClauseForFetchingOnlyChangedDataFromDB = string.Format(WhereClauseForFetchingOnlyChangedDataFromDB, props + " != null and {0}", prop + ".{1}");
                    }

                    if (!string.IsNullOrEmpty(props))
                        props += ".";
                }

                string timeSatmpCondition;
                if (isForServerSide)
                    timeSatmpCondition = "TimeStamp == @{0}";
                else
                    timeSatmpCondition = "TimeStampUnit > @{0}";

                if (WhereClauseForFetchingOnlyChangedDataFromDB != null)
                {
                    //WhereClauseForFetchingOnlyChangedDataFromDB = WhereClauseForFetchingOnlyChangedDataFromDB.Replace(".{0}", "{0}");
                    WhereClauseForFetchingOnlyChangedDataFromDB = string.Format(WhereClauseForFetchingOnlyChangedDataFromDB, string.Empty, timeSatmpCondition);
                    if (navData.Count() == 0)
                        return;
                    CalcTimeStamp(navData.ToList(), out maxTimeStampUnit, out maxTimeStamp);
                    maxTimeStamp = BitConverter.GetBytes(maxTimeStampUnit).Reverse().ToArray();
                    ulong oldMaxTimeStampUnit;
                    if (cacheInfo.MaxTimeStamesDic.TryGetValue(key, out oldMaxTimeStampUnit))
                    {
                        if (maxTimeStampUnit > oldMaxTimeStampUnit)
                            cacheInfo.MaxTimeStamesDic[key] = maxTimeStampUnit;
                    }
                    else
                    {
                        cacheInfo.MaxTimeStamesDic[key] = maxTimeStampUnit;
                    }

                    cacheInfo.WhereClauseForFetchingOnlyChangedDataFromDB_Dic[key] = WhereClauseForFetchingOnlyChangedDataFromDB;
                }
            }
        }

        /// <summary>
        ///
        /// </summary>
        /// <param name="result"></param>
        /// <param name="cacheInfo"></param>
        /// <param name="isForServerSide">this is 'true' if must excecute in cache server...</param>
        /// <returns></returns>
        public static IQueryable MakeQueryableForFetchingOnlyChangedDataFromDB(IQueryable result, CacheInfo cacheInfo, bool isForServerSide)
        {
            if (cacheInfo.EnableToFetchOnlyChangedDataFromDB && cacheInfo.MaxTimeStamp != null)
            {
                var parames = new List<object>();
                string whereConditionString;
                if (isForServerSide)
                    whereConditionString = "@0 == TimeStamp";
                else
                    whereConditionString = "@0 < TimeStampUnit";

                var counter = 0;

                if (isForServerSide)
                    parames.Add(cacheInfo.MaxTimeStamp);
                else
                    parames.Add(cacheInfo.MaxTimeStampUint);

                var strTimeStampForDic = ConvertToStringFromTimeStamp(cacheInfo.MaxTimeStamp);
                var strTimeStampLst = new List<string>();
                strTimeStampLst.Add(strTimeStampForDic);
                if (!string.IsNullOrWhiteSpace(cacheInfo.NameOfNavigationPropsForFetchingOnlyChangedDataFromDB))
                {
                    foreach (var propStr in cacheInfo.NameOfNavigationPropsForFetchingOnlyChangedDataFromDB.Split(','))
                    {
                        ulong timeStamp;
                        if (cacheInfo.MaxTimeStamesDic.TryGetValue(propStr, out timeStamp))
                        {
                            counter++;
                            whereConditionString += string.Format(cacheInfo.WhereClauseForFetchingOnlyChangedDataFromDB_Dic[propStr], counter);
                            var ts = BitConverter.GetBytes(timeStamp).Reverse().ToArray();
                            if (isForServerSide)
                                parames.Add(ts);
                            else
                                parames.Add(timeStamp);

                            var strTimeStampForDic1 = ConvertToStringFromTimeStamp(ts);
                            strTimeStampLst.Add(strTimeStampForDic1);
                        }
                    }

                    //  var result1 = result.ToList().AsQueryable();
                    //  var a = result1.Where(whereConditionString, parames.ToArray());
                }

                result = result.Where(whereConditionString, parames.ToArray());

                if (isForServerSide)
                    TimeStamps[result.ToString().GetHashCode()] = strTimeStampLst.ToArray();
            }
            return result;
        }

        protected virtual string GenerateCacheKeyBase()
        {
            return CacheInfo.BasicKey.ToString();
        }

        public object GetDataFromCacheServer()
        {
            if (CacheInfo.EnableUseCacheServer)
            {
                if (!ConfigHelper.GetConfigValue<bool>("IsCacheServer"))
                {
                    return GetDataFromCacheServerViaWCF();
                }
            }
            return null;
        }

        public virtual void SetFunc(object func)
        {
        }

        protected abstract object DeserializeCacheData(byte[] result);

        private static void CalcTimeStamp(List<ObjectBase> resultList, out ulong maxTimeStampUnit, out byte[] maxTimeStamp)
        {
            maxTimeStampUnit = resultList.Max(item => item.TimeStampUnit);
            maxTimeStamp = BitConverter.GetBytes(maxTimeStampUnit).Reverse().ToArray();
        }

        private static string ConvertToStringFromTimeStamp(byte[] timeStamp)
        {
            return "0x" + BitConverter.ToString(timeStamp).Replace("-", string.Empty);
        }

        private object GetDataFromCacheServerViaWCF()
        {
            EndpointAddress endpointAddress = new EndpointAddress(ConfigHelper.GetConfigValue<string>("CacheClientWebServiceUrl"));
            NetTcpBinding binding = new NetTcpBinding();
            binding.MaxBufferPoolSize = int.MaxValue;
            binding.MaxReceivedMessageSize = int.MaxValue;
            binding.MaxBufferSize = int.MaxValue;
            // binding.TransferMode = TransferMode.Streamed;
            binding.ReaderQuotas.MaxArrayLength = int.MaxValue;
            binding.ReaderQuotas.MaxBytesPerRead = int.MaxValue;
            binding.ReaderQuotas.MaxDepth = int.MaxValue;
            binding.ReaderQuotas.MaxNameTableCharCount = int.MaxValue;
            binding.ReaderQuotas.MaxStringContentLength = int.MaxValue;
            binding.Security.Mode = SecurityMode.None;
            binding.CloseTimeout = new TimeSpan(0, 2, 0);
            binding.OpenTimeout = new TimeSpan(0, 2, 0);
            binding.ReceiveTimeout = new TimeSpan(0, 2, 0);
            binding.SendTimeout = new TimeSpan(0, 2, 0);
            binding.Security.Message.ClientCredentialType = MessageCredentialType.None;
            binding.Security.Transport.ClientCredentialType = TcpClientCredentialType.None;
            using (ServiceClient proxy = new ServiceClient(binding, endpointAddress))
            {
                var logerService = AppBase.LogService;
                try
                {
                    var result = proxy.GetCacheDataViaWcf(this);
                    if (CacheInfo.EnableCoreSerialization)
                    {
                        result = DeserializeCacheData((byte[])result);
                    }

                    proxy.Close();
                    if (CacheInfo.EnableToFetchOnlyChangedDataFromDB)
                        QueryableCacheDataProvider<ObjectBase>.CalcAllTimeStampAndSet(result as IEnumerable, CacheInfo, false);
                    return result;
                }
                catch (FaultException exc)
                {
                    if (exc is System.ServiceModel.FaultException<System.ServiceModel.ExceptionDetail>)
                    {
                        var customEx = new CustomCacheFaultedException((exc as FaultException<ExceptionDetail>).Detail);
                        //var eLog = logerService.GetEventLogObj();
                        //eLog.OccuredException = customEx;
                        //eLog.UserId = "Cache";
                        //eLog.CustomMessage = "cache server error report in client side!";
                        //logerService.Handle(eLog);

                        proxy.Abort();
                        throw logerService.Handle(customEx, "cache server error report in client side!", true, "Cache");

                        //throw customEx;
                    }
                    else
                    {
                        //var eLog = logerService.GetEventLogObj();
                        //eLog.OccuredException = exc;
                        //eLog.UserId = "Cache";
                        //eLog.CustomMessage = "cache server error report in client side!";
                        //logerService.Handle(eLog);
                        proxy.Abort();
                        throw logerService.Handle(exc, "cache server error report in client side!", true, "Cache");

                        //throw exc;
                    }
                }
                catch (Exception ex)
                {
                    //var eLog = logerService.GetEventLogObj();
                    //eLog.OccuredException = ex;
                    //eLog.UserId = "Cache";
                    //eLog.CustomMessage = "cache server error report in client side!";
                    //logerService.Handle(eLog);
                    proxy.Abort();
                    //throw ex;
                    throw logerService.Handle(ex, "cache server error report in client side!", true, "Cache");
                }
            }
        }
        public string GenerateCacheKey()
        {
            var culture = System.Threading.Thread.CurrentThread.CurrentCulture;
            try
            {
                System.Threading.Thread.CurrentThread.CurrentCulture = DefaultCultureInfo;
                return GenerateCacheKeyBase();
            }
            finally
            {
                System.Threading.Thread.CurrentThread.CurrentCulture = culture;
            }
        }
    }

    [DataContract]
    public abstract class CacheDataProvider<T> : CacheDataProvider, ICacheDataProvider<T>
    {
        public CacheDataProvider(CacheInfo cacheInfo) : base(cacheInfo)
        {
            CacheInfo = cacheInfo;
        }

        public T GetFreshData()
        {
            T resultList;
            T hDDResult;
            if (CacheInfo.EnableSaveCacheOnHDD && CacheInfo.NotYetGetCacheData && (CacheInfo.CacheRefreshingKind != CacheRefreshingKind.SqlDependency || (CacheInfo.EnableToFetchOnlyChangedDataFromDB)) && TryGetDataFromHDD(out hDDResult))
            {
                resultList = hDDResult;
            }
            else
            if (CacheInfo.EnableUseCacheServer && !ConfigHelper.GetConfigValue<bool>("IsCacheServer"))
            {
                resultList = (T)GetDataFromCacheServer();
            }
            else
            {
                resultList = ExcecuteCacheMethod();
            }

            if (resultList is IList)
            {
                CacheInfo.LastRecordCount = (resultList as IList).Count;
            }

            return resultList;
        }

        protected override object DeserializeCacheData(byte[] result)
        {
            return BinaryConverter.Deserialize<T>(result);
        }

        protected abstract T ExcecuteCacheMethod();

        protected virtual bool TryGetDataFromHDD(out T cacheData)
        {
            var cacheFilePath = string.Format(System.AppDomain.CurrentDomain.BaseDirectory + @"/Cache/{0}.Cache", CacheInfo.Name);
            try
            {
                if (System.IO.File.Exists(cacheFilePath))
                {
                    var text = System.IO.File.ReadAllBytes(cacheFilePath);
                    var obj = Core.Cmn.Extensions.SerializationExtensions.DeSerializeBinaryToObject<T>(text);
                    cacheData = obj;
                    return true;
                }
                cacheData = default(T);
                return false;
            }
            catch (Exception ex)
            {
                AppBase.LogService.Handle(ex, $"On load cache from HDD an error occured.CacheName is: {CacheInfo.Name} and cache file path in HDD is: {cacheFilePath}");
                cacheData = default(T);
                return false;
            }
        }
    }

    public class CustomCacheFaultedException : Exception
    {
        private string _stackTrace;

        public CustomCacheFaultedException(ExceptionDetail exceptionDetail)
                    : base(exceptionDetail.Message, exceptionDetail.InnerException == null ?
            new CustomCacheFaultedException() : new CustomCacheFaultedException(exceptionDetail.InnerException))
        {
            _stackTrace = exceptionDetail.StackTrace;
        }

        private CustomCacheFaultedException()
            : base()
        {
            _stackTrace = string.Empty;
        }

        public override string StackTrace
        {
            get
            {
                return _stackTrace;
            }
        }
    }

    [DataContract]
    public class QueryableCacheDataProvider<T> : CacheDataProvider<List<T>>, IQueryableCacheDataProvider
    {
        private IDbContextBase _dbContext;

        public QueryableCacheDataProvider(CacheInfo cacheInfo)
                    : base(cacheInfo)
        {
            CacheInfo = cacheInfo;
        }
        public IDbContextBase DbContext
        {
            get
            {
                if (_dbContext == null)
                    _dbContext = AppBase.DependencyInjectionFactory.CreateContextInstance();
                return _dbContext;
            }
            set
            {
                _dbContext = value;
            }
        }

        public virtual IQueryable GetQuery()
        {
            return (IQueryable)CacheInfo.MethodInfo.Invoke(null, new object[] { CacheInfo.Repository.GetQueryableForCahce(DbContext) });
        }

        protected override List<T> ExcecuteCacheMethod()
        {
            List<T> resultList;
            IQueryable<T> result = (IQueryable<T>)GetQuery();
            result = MakeQueryableForFetchingOnlyChangedDataFromDB(result, CacheInfo, true) as IQueryable<T>;
            CacheInfo.LastQueryStringOnlyForQueryableCache = result.ToString();
            CacheInfo.MaxTimeStampCopy = CacheInfo.Repository.GetMaxTimeStamp();
            if (CacheInfo.MaxTimeStampCopy.Count() < 8)
                CacheInfo.MaxTimeStampUintCopy = 0;
            else
                CacheInfo.MaxTimeStampUintCopy = BitConverter.ToUInt64(CacheInfo.MaxTimeStampCopy.Reverse().ToArray(), 0);
            if (CacheInfo.MaxTimeStampUintCopy == CacheInfo.MaxTimeStampUint && string.IsNullOrEmpty(CacheInfo.NameOfNavigationPropsForFetchingOnlyChangedDataFromDB))
                resultList = new List<T>();
            else
                resultList = result.ToList();
            return resultList;
        }

        //Todo:Caches with parameters must be saved on HDD. Use a dictionary for them and serialize then deserialize, after that restor on cache again.
        protected override bool TryGetDataFromHDD(out List<T> cacheData)
        {
            if (base.TryGetDataFromHDD(out cacheData))
            {
                return true;
            }
            else
                return false;
        }
    }

    [DataContract]
    public class QueryableCacheDataProvider<T, P1> : QueryableCacheDataProvider<T>
    {
        public QueryableCacheDataProvider(CacheInfo cacheInfo, P1 param1)
            : base(cacheInfo)
        {
            CacheInfo = cacheInfo;
            Param1 = param1;
        }

        [DataMember]
        public P1 Param1 { get; set; }

        protected override string GenerateCacheKeyBase()
        {
            return $"{base.GenerateCacheKeyBase()}_{Param1}";
        }

        public override IQueryable GetQuery()
        {
            var result = (IQueryable)CacheInfo.MethodInfo.Invoke(null, new object[] { CacheInfo.Repository.GetQueryableForCahce(DbContext), Param1 });
            return result;
        }
    }

    [DataContract]
    public class QueryableCacheDataProvider<T, P1, P2> : QueryableCacheDataProvider<T>
    {
        public QueryableCacheDataProvider(CacheInfo cacheInfo, P1 param1, P2 param2)
            : base(cacheInfo)
        {
            CacheInfo = cacheInfo;
            Param1 = param1;
            Param2 = param2;
        }

        [DataMember]
        public P1 Param1 { get; set; }

        [DataMember]
        public P2 Param2 { get; set; }

        protected override string GenerateCacheKeyBase()
        {
            return $"{base.GenerateCacheKeyBase()}_{Param1}_{Param2}";
        }

        public override IQueryable GetQuery()
        {
            var result = (IQueryable)CacheInfo.MethodInfo.Invoke(null, new object[] { CacheInfo.Repository.GetQueryableForCahce(DbContext), Param1, Param2 });
            return result;
        }
    }

    [DataContract]
    public class QueryableCacheDataProvider<T, P1, P2, P3> : QueryableCacheDataProvider<T>
    {
        public QueryableCacheDataProvider(CacheInfo cacheInfo, P1 param1, P2 param2, P3 param3)
            : base(cacheInfo)
        {
            CacheInfo = cacheInfo;
            Param1 = param1;
            Param2 = param2;
            Param3 = param3;
        }

        [DataMember]
        public P1 Param1 { get; set; }

        [DataMember]
        public P2 Param2 { get; set; }

        [DataMember]
        public P3 Param3 { get; set; }

        protected override string GenerateCacheKeyBase()
        {
            return $"{base.GenerateCacheKeyBase()}_{Param1}_{Param2}_{Param3}";
        }

        public override IQueryable GetQuery()
        {
            var result = (IQueryable)CacheInfo.MethodInfo.Invoke(null, new object[] { CacheInfo.Repository.GetQueryableForCahce(DbContext), Param1, Param2, Param3 });
            return result;
        }
    }

    [DataContract]
    public class QueryableCacheDataProvider<T, P1, P2, P3, P4> : QueryableCacheDataProvider<T>
    {
        public QueryableCacheDataProvider(CacheInfo cacheInfo, P1 param1, P2 param2, P3 param3, P4 param4)
            : base(cacheInfo)
        {
            CacheInfo = cacheInfo;
            Param1 = param1;
            Param2 = param2;
            Param3 = param3;
            Param4 = param4;
        }

        [DataMember]
        public P1 Param1 { get; set; }

        [DataMember]
        public P2 Param2 { get; set; }

        [DataMember]
        public P3 Param3 { get; set; }

        [DataMember]
        public P4 Param4 { get; set; }

        protected override string GenerateCacheKeyBase()
        {
            return $"{base.GenerateCacheKeyBase()}_{Param1}_{Param2}_{Param3}_{Param4}";
        }

        public override IQueryable GetQuery()
        {
            var result = (IQueryable)CacheInfo.MethodInfo.Invoke(null, new object[] { CacheInfo.Repository.GetQueryableForCahce(DbContext), Param1, Param2, Param3, Param4 });
            return result;
        }
    }
}