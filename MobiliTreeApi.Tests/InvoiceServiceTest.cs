using System;
using System.Linq;
using MobiliTreeApi.Repositories;
using MobiliTreeApi.Services;
using Xunit;

namespace MobiliTreeApi.Tests
{
    public class InvoiceServiceTest
    {
        private readonly ISessionsRepository _sessionsRepository;
        private readonly IParkingFacilityRepository _parkingFacilityRepository;
        private readonly ICustomerRepository _customerRepository;

        public InvoiceServiceTest()
        {
            _sessionsRepository = new SessionsRepositoryFake(FakeData.GetSeedSessions());
            _parkingFacilityRepository = new ParkingFacilityRepositoryFake(FakeData.GetSeedServiceProfiles());
            _customerRepository = new CustomerRepositoryFake(FakeData.GetSeedCustomers());
        }

        [Fact]
        public void GivenSessionsService_WhenQueriedForInexistentParkingFacility_ThenThrowException()
        {
            var ex = Assert.Throws<ArgumentException>(() => GetSut().GetInvoices("nonExistingParkingFacilityId"));
            Assert.Equal("Invalid parking facility id 'nonExistingParkingFacilityId'", ex.Message);
        }

        [Fact]
        public void GivenEmptySessionsStore_WhenQueriedForUnknownParkingFacility_ThenReturnEmptyInvoiceList()
        {
            var result = GetSut().GetInvoices("pf001");

            Assert.Empty(result);
        }

        [Fact]
        public void GivenOneSessionInTheStore_WhenQueriedForExistingParkingFacility_ThenReturnInvoiceListWithOneElement()
        {
            var startDateTime = new DateTime(2018, 12, 15, 12, 25, 0);
            _sessionsRepository.AddSession(new Domain.Session
            {
                CustomerId = "some customer",
                ParkingFacilityId = "pf001",
                StartDateTime = startDateTime,
                EndDateTime = startDateTime.AddHours(1)
            });

            var result = GetSut().GetInvoices("pf001");
            
            var invoice = Assert.Single(result);
            Assert.NotNull(invoice);
            Assert.Equal("pf001", invoice.ParkingFacilityId);
            Assert.Equal("some customer", invoice.CustomerId);
        }

        [Fact]
        public void GivenMultipleSessionsInTheStore_WhenQueriedForExistingParkingFacility_ThenReturnOneInvoicePerCustomer()
        {
            var startDateTime = new DateTime(2018, 12, 15, 12, 25, 0);
            _sessionsRepository.AddSession(new Domain.Session
            {
                CustomerId = "c001",
                ParkingFacilityId = "pf001",
                StartDateTime = startDateTime,
                EndDateTime = startDateTime.AddHours(1)
            });
            _sessionsRepository.AddSession(new Domain.Session
            {
                CustomerId = "c001",
                ParkingFacilityId = "pf001",
                StartDateTime = startDateTime,
                EndDateTime = startDateTime.AddHours(1)
            });
            _sessionsRepository.AddSession(new Domain.Session
            {
                CustomerId = "c002",
                ParkingFacilityId = "pf001",
                StartDateTime = startDateTime,
                EndDateTime = startDateTime.AddHours(1)
            });

            var result = GetSut().GetInvoices("pf001");

            Assert.Equal(2, result.Count);
            var invoiceCust1 = result.SingleOrDefault(x => x.CustomerId == "c001");
            var invoiceCust2 = result.SingleOrDefault(x => x.CustomerId == "c002");
            Assert.NotNull(invoiceCust1);
            Assert.NotNull(invoiceCust2);
            Assert.Equal("pf001", invoiceCust1.ParkingFacilityId);
            Assert.Equal("pf001", invoiceCust2.ParkingFacilityId);
            Assert.Equal("c001", invoiceCust1.CustomerId);
            Assert.Equal("c002", invoiceCust2.CustomerId);
        }

        [Fact]
        public void GivenMultipleSessionsForMultipleFacilitiesInTheStore_WhenQueriedForExistingParkingFacility_ThenReturnInvoicesOnlyForQueriedFacility()
        {
            var startDateTime = new DateTime(2018, 12, 15, 12, 25, 0);
            _sessionsRepository.AddSession(new Domain.Session
            {
                CustomerId = "c001",
                ParkingFacilityId = "pf001",
                StartDateTime = startDateTime,
                EndDateTime = startDateTime.AddHours(1)
            });
            _sessionsRepository.AddSession(new Domain.Session
            {
                CustomerId = "c001",
                ParkingFacilityId = "pf002",
                StartDateTime = startDateTime,
                EndDateTime = startDateTime.AddHours(1)
            });
            _sessionsRepository.AddSession(new Domain.Session
            {
                CustomerId = "c002",
                ParkingFacilityId = "pf001",
                StartDateTime = startDateTime,
                EndDateTime = startDateTime.AddHours(1)
            });

            var result = GetSut().GetInvoices("pf001");

            Assert.Equal(2, result.Count);
            var invoiceCust1 = result.SingleOrDefault(x => x.CustomerId == "c001");
            var invoiceCust2 = result.SingleOrDefault(x => x.CustomerId == "c002");
            Assert.NotNull(invoiceCust1);
            Assert.NotNull(invoiceCust2);
            Assert.Equal("pf001", invoiceCust1.ParkingFacilityId);
            Assert.Equal("pf001", invoiceCust2.ParkingFacilityId);
            Assert.Equal("c001", invoiceCust1.CustomerId);
            Assert.Equal("c002", invoiceCust2.CustomerId);
        }

        [Fact]
        public void GivenSessionThatSpansMultipleTariffSlots_WhenQueriedForInvoices_ThenCorrectTotalAmountIsCalculated()
        {
            var startDateTime = new DateTime(2018, 12, 15, 06, 00, 0); 
            var endDateTime = new DateTime(2018, 12, 15, 10, 00, 0);   

            _sessionsRepository.AddSession(new Domain.Session
            {
                CustomerId = "c003",
                ParkingFacilityId = "pf001",
                StartDateTime = startDateTime,
                EndDateTime = endDateTime
            });


            var result = GetSut().GetInvoices("pf001");

            var invoice = Assert.Single(result);
            Assert.Equal("c003", invoice.CustomerId);

            //  - 06:00–07:00 = €0.8/uur
            //  - 07:00–10:00 = €2.8/uur
            // Totaal = 0.8 + 2.8 + 2.8 + 2.8 = €9.2
            Assert.Equal(9.2m, invoice.Amount);
        }

        [Fact]
        public void GivenSessionThatSpansWeekendIntoWeekday_WhenQueriedForInvoices_ThenCalculateWithCombinedTariffs()
        {
            var startDateTime = new DateTime(2018, 12, 16, 21, 0, 0);
            var endDateTime = new DateTime(2018, 12, 17, 10, 0, 0);  

            _sessionsRepository.AddSession(new Domain.Session
            {
                CustomerId = "c004",
                ParkingFacilityId = "pf001",
                StartDateTime = startDateTime,
                EndDateTime = endDateTime
            });

            var result = GetSut().GetInvoices("pf001");

            var invoice = Assert.Single(result);

            Assert.Equal("c004", invoice.CustomerId);

            // Totaal: 13 uur van zondag tot maandag
            // Zondag 21–24 (3u) — weekend prijs (1.8€/uur)
            // Maandag 00–07 (7u) — week prijs (e.g., 0.5€/uur)
            // Maandag 07–10 (3u) — week prijs (e.g., 2.5€/uur)
            // = (3 * 1.8) + (7 * 0.5) + (3 * 2.5) = 16.4
            Assert.Equal(16.4m, invoice.Amount);
        }



        private IInvoiceService GetSut()
        {
            return new InvoiceService(
                _sessionsRepository, 
                _parkingFacilityRepository,
                _customerRepository);
        }
    }
}
