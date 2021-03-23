﻿using API.DTO;
using API.Entities;
using API.Helpers;
using API.Interfaces;
using AutoMapper;
using AutoMapper.QueryableExtensions;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace API.Data
{
    public class MessageRepository : IMessageRepository
    {
        private readonly DataContext _context;
        private readonly IMapper _mapper;

        public MessageRepository(DataContext context, IMapper mapper)
        {
            _context = context;
            _mapper = mapper;
        }

        public void AddMessage(Message message)
        {
            _context.Messsages.Add(message);
        }

        public void DeleteMessage(Message message)
        {
            _context.Messsages.Remove(message);
        }

        public async Task<Message> GetMessage(int id)
        {
            return await _context.Messsages
                .Include(u => u.Sender)
                .Include(u => u.Recipient)
                .SingleOrDefaultAsync(x => x.Id == id);
        }

        public async Task<PagedList<MessageDto>> GetMessagesForUser(MessageParams messageParams)
        {
            var query = _context.Messsages.OrderByDescending(m => m.MessageSent).AsQueryable();

            query = messageParams.Container switch
            {
                "Inbox" => query.Where(u => u.Recipient.UserName == messageParams.UserName && u.RecipienteDeleted == false),
                "Outbox" => query.Where(u => u.Sender.UserName == messageParams.UserName && u.SenderDeleted == false),
                 _ => query.Where(u => u.Recipient.UserName == messageParams.UserName && u.RecipienteDeleted && u.DateRead == null)
            };

            var messages = query.ProjectTo<MessageDto>(_mapper.ConfigurationProvider);

            return await PagedList<MessageDto>.CreateAsync(messages, messageParams.PageNumber, messageParams.PageSize); 

        }

        public async Task<IEnumerable<MessageDto>> GetMessageThread(string currentUserUserName, string recipientUserName)
        {
            var messages = await _context.Messsages
                .Include(u => u.Sender).ThenInclude(p => p.Photos)
                .Include(u => u.Recipient).ThenInclude(p => p.Photos)
                .Where(m => m.Recipient.UserName == currentUserUserName && m.RecipienteDeleted == false
                        && m.Sender.UserName == recipientUserName
                        || m.Recipient.UserName == recipientUserName
                        && m.Sender.UserName == currentUserUserName && m.SenderDeleted == false
                )
                .OrderBy(m=> m.MessageSent)
                .ToListAsync();

            var unReadMessages = messages
                .Where(m => m.DateRead == null && m.Recipient.UserName == currentUserUserName)
                .ToList();

            if (unReadMessages.Any())
            {
                foreach(var message in unReadMessages)
                {
                    message.DateRead = DateTime.Now;
                }

                await _context.SaveChangesAsync();
            }

            return _mapper.Map<IEnumerable<MessageDto>>(messages);
        }

        public async Task<bool> SaveAllAsync()
        {
            return await _context.SaveChangesAsync() > 0;
        }
    }
}