using System;
using System.Collections.Generic;
using System.Linq;
using MobiliTreeApi.Domain;
using MobiliTreeApi.Repositories;

namespace MobiliTreeApi.Services
{
    public interface IInvoiceService
    {
        List<Invoice> GetInvoices(string parkingFacilityId);
        Invoice GetInvoice(string parkingFacilityId, string customerId);
    }

    public class InvoiceService: IInvoiceService
    {
        private readonly ISessionsRepository _sessionsRepository;
        private readonly IParkingFacilityRepository _parkingFacilityRepository;
        private readonly ICustomerRepository _customerRepository;

        public InvoiceService(ISessionsRepository sessionsRepository, IParkingFacilityRepository parkingFacilityRepository, ICustomerRepository customerRepository)
        {
            _sessionsRepository = sessionsRepository;
            _parkingFacilityRepository = parkingFacilityRepository;
            _customerRepository = customerRepository;
        }

        public List<Invoice> GetInvoices(string parkingFacilityId)
        {
            var serviceProfile = _parkingFacilityRepository.GetServiceProfile(parkingFacilityId);
            if (serviceProfile == null)
            {
                throw new ArgumentException($"Invalid parking facility id '{parkingFacilityId}'");
            }

            var sessions = _sessionsRepository.GetSessions(parkingFacilityId);

            return sessions.GroupBy(x => x.CustomerId).Select(x => new Invoice
            {
                ParkingFacilityId = parkingFacilityId,
                CustomerId = x.Key,
                Amount = x.Sum(session => CalculatePrice(serviceProfile, session))
            }).ToList();
        }

        private decimal CalculatePrice(ServiceProfile serviceProfile, Session session)
        {
            // Controleer of de sessie geldig is (starttijd moet vóór eindtijd liggen)
            if (session.StartDateTime >= session.EndDateTime)
                return 0;

            var startTime = session.StartDateTime;
            var endTime = session.EndDateTime;

            var isWeekend = startTime.DayOfWeek == DayOfWeek.Saturday || startTime.DayOfWeek == DayOfWeek.Sunday;
            var prices = isWeekend ? serviceProfile.WeekendPrices : serviceProfile.WeekDaysPrices;
            int hour = startTime.Hour;

            // Zoek het prijsslot dat overeenkomt met het startuur
            var slot = prices.FirstOrDefault(s => hour >= s.StartHour && hour < s.EndHour);
            if (slot == null)
                throw new Exception($"Geen prijsslot gevonden voor {hour} uur op {(isWeekend ? "weekendtarieven" : "weekdagtarieven")}.");

            var totalHours = (decimal)(endTime - startTime).TotalHours;

            // Reken af op basis van afgeronde uren naar boven (elke begonnen uur telt als volledig)
            return Math.Ceiling(totalHours) * slot.PricePerHour;
        }

        public Invoice GetInvoice(string parkingFacilityId, string customerId)
        {
            throw new NotImplementedException();
        }
    }
}
