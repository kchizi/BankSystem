﻿namespace BankSystem.Services.Implementations
{
    using AutoMapper;
    using AutoMapper.QueryableExtensions;
    using BankSystem.Models;
    using Common.EmailSender.Interface;
    using Data;
    using Interfaces;
    using Microsoft.EntityFrameworkCore;
    using Models.MoneyTransfer;
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading.Tasks;

    public class MoneyTransferService : BaseService, IMoneyTransferService
    {
        private const string EmailReceiveMoneySubject = "You've received money";
        private const string EmailReceiveMoneyMessage = "€{0} have been transferred to your account. Please log in your account for additional information.";
        private const string EmailSendMoneySubject = "You've sent money";

        private const string EmailSendMoneyMessage =
            "€{0} have been transferred from your account. If it was not you, please contact our support center as fast as possible!";

        private readonly IEmailSender emailSender;

        public MoneyTransferService(BankSystemDbContext context, IEmailSender emailSender)
            : base(context)
        {
            this.emailSender = emailSender;
        }

        public async Task<IEnumerable<T>> GetAllMoneyTransfersAsync<T>(string userId)
            where T : MoneyTransferBaseServiceModel
            => await this.Context
                .Transfers
                .Where(t => t.Account.UserId == userId)
                .OrderByDescending(mt => mt.MadeOn)
                .ProjectTo<T>()
                .ToArrayAsync();

        public async Task<IEnumerable<T>> GetAllMoneyTransfersForAccountAsync<T>(string accountId)
            where T : MoneyTransferBaseServiceModel
            => await this.Context
                .Transfers
                .Where(t => t.AccountId == accountId)
                .OrderByDescending(mt => mt.MadeOn)
                .ProjectTo<T>()
                .ToArrayAsync();

        public async Task<IEnumerable<T>> GetLast10MoneyTransfersForUserAsync<T>(string userId)
            where T : MoneyTransferBaseServiceModel
            => await this.Context
                .Transfers
                .Where(mt => mt.Account.UserId == userId)
                .OrderByDescending(mt => mt.MadeOn)
                .Take(10)
                .ProjectTo<T>()
                .ToArrayAsync();

        public async Task<bool> CreateMoneyTransferAsync<T>(T model)
            where T : MoneyTransferBaseServiceModel
        {
            if (!this.IsEntityStateValid(model))
            {
                return false;
            }

            var dbModel = Mapper.Map<MoneyTransfer>(model);
            var userAccount = await this.Context
                .Accounts
                .Include(u=> u.User)
                .Where(u => u.Id == dbModel.AccountId)
                .SingleOrDefaultAsync();
            if (userAccount == null)
            {
                return false;
            }

            userAccount.Balance += dbModel.Amount;
            this.Context.Update(userAccount);

            await this.Context.Transfers.AddAsync(dbModel);
            await this.Context.SaveChangesAsync();

            if (dbModel.Amount > 0)
            {
                await this.emailSender.SendEmailAsync(dbModel.Account.User.Email, EmailReceiveMoneySubject,
                    string.Format(EmailReceiveMoneyMessage, dbModel.Amount));
            }
            else
            {
                await this.emailSender.SendEmailAsync(dbModel.Account.User.Email, EmailSendMoneySubject,
                    string.Format(EmailSendMoneyMessage, Math.Abs(dbModel.Amount)));
            }

            return true;
        }
    }
}
