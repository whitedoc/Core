﻿using Core.Cmn;
using Core.Cmn.Attributes;
using Core.Cmn.Extensions;
using Core.Ef;
using Core.Entity;
using System;
using System.Collections.Generic;
using System.Data.SqlClient;
using System.Linq;
using System.Text;

namespace Core.Rep
{
    public class ViewElementRepository : RepositoryBase<ViewElement>
    {
        private IDbContextBase _dc;

        public ViewElementRepository(IDbContextBase dbContext)
            : base(dbContext)
        {
            _dc = dbContext;
            dbContext.Configuration.LazyLoadingEnabled = true;
            dbContext.Configuration.ProxyCreationEnabled = true;
        }

        [Cacheable(
                EnableSaveCacheOnHDD = true,
                EnableUseCacheServer = false,
                AutoRefreshInterval = 3600,
                CacheRefreshingKind = Cmn.Cache.CacheRefreshingKind.SqlDependency,
                // این گزینه فقط در صورت استفاده از اسکریپ ویوالمنت ها ست میشود
                // درصورت استفاده از پنل برای مدریت ویوالمنتها باید فالس شود
                DisableToSyncDeletedRecord_JustIfEnableToFetchOnlyChangedDataFromDB = true,
                EnableToFetchOnlyChangedDataFromDB = true,
                EnableCoreSerialization = true
        )]
        public static IQueryable<ViewElement> AllViewElementsCache(IQueryable<ViewElement> query)
        {
            return query;
        }

        public IQueryable<ViewElement> AllCache(bool canUseCacheIfPossible = true)
        {
            return Cache(AllViewElementsCache, canUseCacheIfPossible);
        }

        public IQueryable<ViewElement> All_ChildViewElement()
        {
            //var c = (DbContext)ContextBase;
            //c.Database.ExecuteSqlCommand("");
            return All().Include(x => x.ChildrenViewElement);
        }

        //public override ViewElement Create(ViewElement t, bool allowSaveChange = true)
        //{
        //    ViewElement k = null;
        //    if (t.Id != 0)
        //    {
        //        using (var transaction =(base.ContextBase as DbContextBase).Database.BeginTransaction())
        //        {
        //            (base.ContextBase as DbContextBase).Database.ExecuteSqlCommand("SET IDENTITY_INSERT [core].[ViewElements] ON");
        //            k = base.Create(t, allowSaveChange);
        //            (base.ContextBase as DbContextBase).Database.ExecuteSqlCommand("SET IDENTITY_INSERT [core].[ViewElements] OFF");
        //            transaction.Commit();
        //        }

        //    }
        //    else
        //    {
        //        k = base.Create(t, allowSaveChange);
        //    }

        //    //var db =DependencyInjectionFactory.CreateInjectionInstance<IDbContextBase>() as DbContextBase;
        //    //    db.Database.ExecuteSqlCommand("SET IDENTITY_INSERT [core].[ViewElements] ON");

        //    //(DependencyInjectionFactory.CreateInjectionInstance<IDbContextBase>() as DbContext).Database.ExecuteSqlCommand("SET IDENTITY_INSERT [core].[ViewElements] OFF");
        //    //(base.ContextBase as DbContextBase).Database.ExecuteSqlCommand("SET IDENTITY_INSERT [core].[ViewElements] OFF");

        //    return k;
        //}
        public ViewElement GetViewElementAndChildsById(int id)
        {
            return All_ChildViewElement().FirstOrDefault(viewElement => viewElement.Id == id);
        }

        public IQueryable<ViewElement> GetRootViewElements()
        {
            return All_ChildViewElement().Where(viewElement => viewElement.ParentId == null);
        }

        public ViewElement GetViewElementAndViewElementChildByID(int viewElemntId)
        {
            return All_ChildViewElement().FirstOrDefault(viewElement => viewElement.Id == viewElemntId);
        }

        public IQueryable<ViewElement> GetViewElement(int? id)
        {
            if (id.HasValue)
            {
                return ContextBase.Set<ViewElement>().Where(a => a.ParentId == id && a.IsHidden != true && (!a.InVisible));
            }

            return ContextBase.Set<ViewElement>().Where(a => a.ParentId == null && a.IsHidden != true && (!a.InVisible));
        }

        public int GetNewViewElementId()
        {
            return All().Max(vm => vm.Id) + 1;
        }

        private List<ViewElement> GetHiddenEelements(ViewElement parentElement)
        {
            List<ViewElement> resultElements = new List<ViewElement>();
            var hiddenEl = parentElement.ChildrenViewElement.FirstOrDefault(element => element.IsHidden);
            if (hiddenEl != null) resultElements.Add(hiddenEl);

            if (parentElement.ChildrenViewElement != null)
                foreach (var childEl in parentElement.ChildrenViewElement)
                {
                    var hElement = GetHiddenEelements(childEl);
                    resultElements.AddRange(hElement);
                }
            return resultElements;
        }

        private ViewElement HasHidenField(ViewElement vElement)
        {
            if (vElement.IsHidden)
            {
                return vElement;
            }

            if (vElement.ChildrenViewElement != null)
                foreach (var childElement in vElement.ChildrenViewElement)
                {
                    if (HasHidenField(childElement) != null) return childElement;
                }

            return null;
        }

        public IQueryable<ViewElement> GetChildViewElementByParentId(int? parentId)
        {
            return All_ChildViewElement().Where(viewElemnt => viewElemnt.ParentId == parentId);
        }

        public IQueryable<ViewElement> GetAllViewElement(int? id)
        {
            //if (id.HasValue)
            //{
            //    return ContextBase.Set<ViewElement>().Where(a => a.ParentId == id);
            //}

            return All_ChildViewElement().Where(a => a.ParentId == id);
        }

        public bool IsDuplicateUniqueName(string uniqueName, int viewElementId)
        {
            if (ContextBase.Set<ViewElement>().Any(a => a.UniqueName == uniqueName && a.Id != viewElementId))
                return true;
            return false;
        }

        public int Delete(int id, string userName)
        {
            var dtResult = CheckRelationBeforeDelete(string.Format("{0}.{1}", Schema, TableName), KeyName, id.ToString());
            if (dtResult.Count >= 1)
            {
                var s = new StringBuilder();
                s.Append(" این رکورد در جداول");
                s.Append("<br/>");
                foreach (var item in dtResult)
                {
                    s.Append("<br/>");
                    s.Append(item.inUsedTbName);
                    s.Append("<br/>");
                }
                s.Append("در حال استفاده می باشد");
                throw new Exception(s.ToString(), new Exception(s.ToString(), null));
            }

            var sqlParams = new List<SqlParameter>();
            var user =
             ContextBase.Set<User>().AsNoTracking().Include("UserProfile").SingleOrDefault(a => a.UserProfile.UserName.ToLower() == userName.ToLower());

            sqlParams.Add(new SqlParameter()
            {
                ParameterName = "@ViewElementId ",
                SqlValue = id
            });

            sqlParams.Add(new SqlParameter()
            {
                ParameterName = "@currentUserId",
                SqlValue = user.Id
            });

            return (int)ExecuteSpRp.ExecuteSp(GeneralSpName.ViewElementMenu, sqlParams, (IDbContextBase)ContextBase);
        }

        public ViewElement GetViewElementByUniqueName(string uniqueName)
        {
            return All().FirstOrDefault(viewElement => viewElement.UniqueName == uniqueName);
        }
    }
}