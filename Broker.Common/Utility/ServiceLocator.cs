using System;
using Microsoft.Extensions.DependencyInjection;

namespace Broker.Common.Utility
{
    public class ServiceLocator
    {
        
        // variables
        private ServiceProvider _currentServiceProvider;
        private static ServiceProvider _serviceProvider;


        // properties
        public static ServiceLocator Current
        {
            get
            {
                return new ServiceLocator(_serviceProvider);
            }
        }
      

        // init
        public ServiceLocator(ServiceProvider currentServiceProvider)
        {
            _currentServiceProvider = currentServiceProvider;
        }
    

        // functions
        public static void SetLocatorProvider(ServiceProvider serviceProvider)
        {
            _serviceProvider = serviceProvider;
        }
        public object GetInstance(Type serviceType)
        {
            return _currentServiceProvider.GetService(serviceType);
        }
        public TService GetInstance<TService>()
        {
            return _currentServiceProvider.GetService<TService>();
        }
 
    }

}