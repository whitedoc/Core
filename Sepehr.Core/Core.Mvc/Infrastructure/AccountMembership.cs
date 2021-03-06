using Core.Service;
using System;
using System.Web.Security;
using Core.Rep.DTO;

namespace Core.Mvc.Infrastructure
{

    public class AccountMembership : IMembership
    {
        private readonly MembershipProvider _provider;


        public AccountMembership()
        {
            _provider = Membership.Provider;
        }

        public int MinPasswordLength
        {
            get
            {
                return _provider.MinRequiredPasswordLength;
            }
        }



        public bool ValidateUser(string userName, string password)
        {
            return _provider.ValidateUser(userName, password);

        }
        public MembershipCreateStatus CreateUser(string userName, string password, string email)
        {
            MembershipCreateStatus status;
            _provider.CreateUser(userName, password, email, null, null, true, null, out status);
            return status;
        }


        public bool ChangePassword(string userName, string oldPassword, string newPassword)
        {
            throw new NotImplementedException();
            //try
            //{
            //    MembershipUser currentUser = _provider.GetUser(userName, true /* userIsOnline */);
            //    return currentUser.ChangePassword(oldPassword, newPassword);
            //}
            //catch (ArgumentException)
            //{
            //    return false;
            //}
            //catch (MembershipPasswordException)
            //{
            //    return false;
            //}
        }


    }
}