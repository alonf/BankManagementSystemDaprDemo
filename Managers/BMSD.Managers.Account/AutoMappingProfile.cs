using AutoMapper;


namespace BMSD.Managers.Account
{
    internal class AutoMappingProfile : Profile
    {
        public AutoMappingProfile()
        {
            CreateMap<Contracts.Requests.CustomerRegistrationInfo, Contracts.Submits.CustomerRegistrationInfo>()
                .ForMember(dest => dest.FullName, opt => opt.MapFrom(src => src.FirstName + " " + src.LastName))
                .ForMember(dest => dest.FullAddress, opt => opt.MapFrom(src =>
                    $"{src.Address}, {src.City}, {src.State} {src.ZipCode}"))
                .ForMember(dest => dest.AccountId, opt => opt.MapFrom(src => Guid.NewGuid().ToString()));
            
            CreateMap<Contracts.Requests.AccountTransactionInfo, Contracts.Submits.AccountTransactionSubmit>();
        }
    }
}
