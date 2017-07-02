﻿using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Web.Mvc;
using Microsoft.CSharp.RuntimeBinder;
using Core.Cmn;
using System.Xml;
using System.Globalization;
using System.Web.Routing;


namespace Core.Mvc.Controller
{
    [ValidateInput(true)]
    public class ControllerBaseCr : System.Web.Mvc.Controller
    {
        private ILogService _logService = Core.Cmn.AppBase.LogService;



        protected override void OnException(System.Web.Mvc.ExceptionContext filterContext)
        {
            //if (filterContext.HttpContext.Request.Headers["X-Requested-With"] != null)
            //{
            //    if (filterContext.HttpContext.Request.Headers["X-Requested-With"].Equals("XMLHttpRequest"))
            //        filterContext.Result = new JsonResult { RecursionLimit = 3, JsonRequestBehavior = System.Web.Mvc.JsonRequestBehavior.DenyGet };
            //}
            //else


            ExceptionInfo excepInfo = new ExceptionInfo(filterContext.Exception, false);
            
            if (!this.HttpContext.User.Identity.Name.ToLower().Equals("admin"))
            {
                var constantService = AppBase.DependencyInjectionManager.Resolve<Service.IConstantService>();
                var applicationFaildMsg = string.Empty;
                constantService.TryGetValue<string>("ApplicationFaild", out applicationFaildMsg);

                excepInfo.Message= applicationFaildMsg/*Core.Resources.ExceptionMessage.ApplicationFaild*/;
                excepInfo.Details = "";
                excepInfo. IsRTL = true;
            }
            
            filterContext.Result = SetException(excepInfo);
            filterContext.ExceptionHandled = true;

            var eLog = _logService.GetEventLogObj();
            eLog.UserId = "ControllerBase";
            eLog.CustomMessage = excepInfo.Message;
            eLog.LogFileName = "ControllerBaseLog";
            eLog.OccuredException = filterContext.Exception;
            _logService.Handle(eLog);
            // log error by elmah

        }

        private ActionResult SetException(ExceptionInfo exception)
        {

            this.HttpContext.Response.Clear();
            
            this.HttpContext.Response.StatusCode = 500;

            this.HttpContext.Response.TrySkipIisCustomErrors = true;
            
            return new JsonResult
            {
                Data = new { Message = exception.Message, Details = exception.Details, IsRTL = exception.IsRTL }
                ,
                RecursionLimit = 3,
                JsonRequestBehavior = System.Web.Mvc.JsonRequestBehavior.AllowGet
            };

        }

        public ActionResult ShowException(ExceptionInfo exception)
        {

            return SetException(exception);


        }

        public void AddModelError(List<ValidationResult> validationResults)
        {
            foreach (var validationResult in validationResults)
            {
                ModelState.AddModelError(validationResult.ErrorMessage, ((string[])(validationResult.MemberNames))[0]);

            }

        }

        [HttpGet]
        public virtual ContentResult CreateHelpView(string viewModelName)
        {
            var filePath = "HelpContent/Help.xml";
            var tokens = this.ControllerContext.RouteData.DataTokens;
            if (tokens != null)
            {
                filePath = string.Format("~/Areas/{0}/{1}", tokens["area"].ToString(), filePath);
            }
            else
            {
                filePath = string.Format("~/{0}", filePath);
            }

            System.Xml.XmlDocument doc = new XmlDocument();

            doc.Load(Server.MapPath(filePath));

            var viewModelHelp = doc.GetElementsByTagName(viewModelName);

            var result = string.Empty;

            foreach (XmlNode item in viewModelHelp)
            {
                result += item.InnerXml;
            }
            return Content(result);
        }


        private CultureInfo SetCurrentCulture(System.Web.Routing.RequestContext requestContext)
        {

            string culture = requestContext.HttpContext.Request.Url.AbsolutePath.Split('/')[1];
            if (culture == "en")
            {
                return System.Threading.Thread.CurrentThread.CurrentUICulture = CultureInfo.CreateSpecificCulture("en-US");
            }
            else if (culture == "ar")
            {
                return System.Threading.Thread.CurrentThread.CurrentUICulture = CultureInfo.CreateSpecificCulture("ar-SA");
            }
            else
            {
                return System.Threading.Thread.CurrentThread.CurrentUICulture = CultureInfo.CreateSpecificCulture("fa-IR");
            }
        }
        
        protected override void Initialize(System.Web.Routing.RequestContext requestContext)
        {
            SetCurrentCulture(requestContext);
            base.Initialize(requestContext);
        }
        //protected override void OnActionExecuting(ActionExecutingContext filterContext)
        //{
        //   // if (filterContext.RouteData.DataTokens["area"].Equals(CoreAreaRegistration.))
        //    object areaName = string.Empty;
            
        //    if (filterContext.RouteData.DataTokens.TryGetValue("area",out areaName) &&
        //        areaName.Equals("Core") && (filterContext.Result is ViewResult || filterContext.Result is PartialViewResult))
        //    {
        //        if (filterContext.Result is ViewResult)
        //        {
        //            var view = filterContext.Result as ViewResult;
        //            view.ViewName =  GenerateViewPath(filterContext.RouteData, view.ViewName);
        //            filterContext.Result = view;
        //        }

        //        else
        //        {
        //            var partialView = filterContext.Result as PartialViewResult;
        //            partialView.ViewName = GenerateViewPath(filterContext.RouteData, partialView.ViewName);
        //            filterContext.Result = partialView;
        //        }
        //    }
        //    base.OnActionExecuting(filterContext);
        //}

        private string GenerateViewPath(RouteData routeData, string viewName)
        {
            
            var controllerName = routeData.GetRequiredString("controller");
            var actionName = routeData.GetRequiredString("action");
            //if (viewName.Equals("Index"))
            //{
            //    viewName = string.Format("~/Areas/Core/{0}/{1}", controllerName, actionName);
            //}
            //else
            //{
            //    viewName = viewName.Replace("~/", "~/Areas/Core/");
            //}
            if(controllerName.ToLower().Equals("partialviews")&& actionName.ToLower().Equals("index"))
            {
                return viewName;
            }

            if (viewName.StartsWith("~/"))
            {
                viewName = viewName.Replace("~/", "~/Areas/Core/");
            }
            else
            {
                if (viewName.Equals(actionName))
                {
                    viewName = string.Format("~/Areas/Core/{0}/{1}", controllerName, actionName);
                }
                else {
                    viewName = string.Format("~/Areas/Core/{0}/{1}", controllerName, viewName);
                
                }

            }
            return viewName;
        }
        //protected override void OnActionExecuting(ActionExecutingContext filterContext)
        //{
        //    base.OnActionExecuting(filterContext);
        //}
        protected override void OnResultExecuted(ResultExecutedContext filterContext)
        {

            object areaName = string.Empty;

            if (filterContext.RouteData.DataTokens.TryGetValue("area", out areaName) &&
                areaName.Equals("Core") && (filterContext.Result is ViewResult || filterContext.Result is PartialViewResult))
            {
                if (filterContext.Result is ViewResult)
                {
                    var view = filterContext.Result as ViewResult;
                    view.ViewName = GenerateViewPath(filterContext.RouteData, view.ViewName);
                    filterContext.Result = view;
                   // view.ExecuteResult(this.ControllerContext);
                }

                else if (filterContext.Result is PartialViewResult)
                {
                    var partialView = filterContext.Result as PartialViewResult;
                    partialView.ViewName = GenerateViewPath(filterContext.RouteData, partialView.ViewName);
                    filterContext.Result = partialView;
                    //partialView.ExecuteResult(this.ControllerContext);

                }
            }
            
            //base.OnResultExecuted(filterContext);
        }
        
    }
}