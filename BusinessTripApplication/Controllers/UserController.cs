﻿using System;
using System.Web.Helpers;
using System.Web.Mvc;
using System.Web.Security;
using BusinessTripApplication.Models;
using BusinessTripModels.Models;
using BusinessTripApplication.Repository;
using BusinessTripApplication.Server;
using BusinessTripApplication.ViewModels;
using Facebook;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace BusinessTripApplication.Controllers
{
    [RequireHttps]
    public class UserController : Controller
    {

        IUserService UserService;
        private static readonly log4net.ILog Logger
       = log4net.LogManager.GetLogger(System.Reflection.MethodBase.GetCurrentMethod().DeclaringType);

        private Uri RedirectUri
        {
            get
            {
                var uriBuilder = new UriBuilder(Request.Url);
                uriBuilder.Query = null;
                uriBuilder.Fragment = null;
                uriBuilder.Path = Url.Action("FacebookCallback");
                return uriBuilder.Uri;
            }
        }

        public UserController(IUserService repo)
        {
            UserService = repo;
        }

        public UserController()
        {
            UserService = new UserService(new UserRepository());
        }

        // GET: User
        [HttpGet]
        public ActionResult Registration()
        {
            if (Request.IsAuthenticated)
            {
                return RedirectToAction("PermissionDenied");
            }
            RegistrationViewModel model = new RegistrationViewModel();
            return View(model);
        }

        [HttpGet]
        public ActionResult LogIn()
        {
            if (Request.IsAuthenticated)
            {
                return RedirectToAction("PermissionDenied");
            }
            LogInViewModel model = new LogInViewModel();
            return View(model);
        }

        [Authorize]
        public ActionResult Dashboard()
        {
            return View();
        }

        [Authorize]
        public ActionResult PermissionDenied()
        {
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult LogIn(string email, string password, bool rememberMe)
        {
            try
            {
                Assert.AreEqual(ModelState.IsValid,true);
                var model = new LogInViewModel(ModelState.IsValid, email, password, rememberMe, UserService, out var response);
                if (response == 1)
                {
                    Response.Cookies.Add(model.Cookie);
                    return RedirectToAction("Index", "Trips");//we want to load a new page with new url, not just rendering the view
                }
                return View(model);
            }
            catch
            {
                return RedirectToRoute("~/Shared/Error");
            }
        }

        [Authorize]
        [HttpPost]
        public ActionResult Logout()
        {
            FormsAuthentication.SignOut();
            return RedirectToAction("Login", "User");
        }


        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult Registration([Bind(Exclude = "IsEmailVerified,ActivationCode")] User user)
        {
            try
            {
                Assert.AreEqual(ModelState.IsValid,true);
                var model = new RegistrationViewModel(ModelState.IsValid, user, UserService);
                return View(model);
            }
            catch (System.Exception e)
            {
                Logger.Info(e.Message);
                return RedirectToRoute("~/Shared/Error");
            }

        }

        [HttpGet]
        public ActionResult VerifyAccount(string id)
        {
            bool result = UserService.VerifyAccount(id);
            ViewBag.Status = result;
            User user = UserService.FindByActivationCode(new Guid(id));
            if (!result)
            {
                if (user!=null && !UserService.IsEmailVerified(user.Email))
                {
                    ViewBag.Message = "Invalid Request!Do you want us to send you another verification email?";
                    ViewBag.Message2 = "Email";
                }
                else ViewBag.Message = "Invalid Request!";
                
                ViewBag.Id = id;
            }
               
                
            else
            {
                Guid guid = new Guid(id);
                LogInViewModel loginVM = new LogInViewModel(UserService, guid);
                Response.Cookies.Add(loginVM.Cookie);
            }
            return View();
        }

        [HttpGet]
        public ActionResult VerifyAccountAgain(string code)
        {
            User user = UserService.FindByActivationCode(new Guid(code));
            UserService.VerifyAccountAgain(code);
            User addedUser = UserService.FindByEmail(user.Email);
            Server.EmailSender emailSender = new EmailSender();
            string url = "https://localhost:44328/User/VerifyAccount/" + addedUser.ActivationCode.ToString();
            string message = "We are excited to tell you that your account is successfully created. " +
                             "Please <a href='" + url + "'>Click here </a> to verify your account. </br>";

            emailSender.SendEmail(addedUser.Email, "Register", message);
            return View();
        }


        [AllowAnonymous]
        public ActionResult login()
        {
            return View();
        }

        public ActionResult logout()
        {
            FormsAuthentication.SignOut();
            return View("Login");
        }

        [AllowAnonymous]
        public ActionResult Facebook()
        {
            var fbLogin = new FacebookLoginer(RedirectUri);
            var redirectLoginUrl = fbLogin.LoginUrl;
            return Redirect(redirectLoginUrl.AbsoluteUri);
        }

        public ActionResult FacebookCallback(string code)
        {
            var fbLogin = new FacebookLoginer(RedirectUri);
            try
            {
                dynamic result = fbLogin.FbClient.Post("oauth/access_token", new
                {
                    client_id = fbLogin.AppId,
                    client_secret = fbLogin.AppSecret,
                    redirect_uri = RedirectUri.AbsoluteUri,
                    code = code
                });
                fbLogin.Response(UserService, result);
                Session["AccessToken"] = result.access_token;

                return RedirectToAction("Index", "Trips");
            }
            catch (Exception e)
            {
                return RedirectToAction("Login", "User");
            }
        }


       

    }

}


