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
            //failsafe voor eventuele fouten op data-vlak.
            if (session.StartDateTime >= session.EndDateTime)
                return 0;

            decimal total = 0;

            var currentTime = session.StartDateTime;
            var endTime = session.EndDateTime;

            //loop per uur van de parkeersessie om te meerdere tarieven in het achterhoofd te houden
            //bij parkeerduur die over verschillende tarief-sloten loopt de som maken van deze
            while (currentTime < endTime)
            {
                var isWeekend = currentTime.DayOfWeek == DayOfWeek.Saturday || currentTime.DayOfWeek == DayOfWeek.Sunday;
                var prices = isWeekend ? serviceProfile.WeekendPrices : serviceProfile.WeekDaysPrices;

                int hour = currentTime.Hour;

                var slot = prices.FirstOrDefault(s => hour >= s.StartHour && hour < s.EndHour);
                if (slot == null)
                    throw new Exception($"No price slot found for hour {hour} on {(isWeekend ? "weekend" : "weekday")}.");

                total += slot.PricePerHour;

                currentTime = currentTime.AddHours(1);
            }

            return total;
        }


        public Invoice GetInvoice(string parkingFacilityId, string customerId)
        {
            throw new NotImplementedException();
        }
    }
}
