﻿using Autofac;
using Autofac.Extensions.DependencyInjection;
using AutoMapper;
using FluentValidation;
using FWT.Infrastructure.Configuration;
using FWT.Infrastructure.Filters;
using FWT.Infrastructure.IdentityServer;
using FWT.Infrastructure.Swagger;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Swashbuckle.AspNetCore.Swagger;
using System;

namespace FWT.Auth
{
    public class Startup
    {
        private readonly IHostingEnvironment _hostingEnvironment;
        private IConfigurationRoot _configuration;

        public Startup(IHostingEnvironment hostingEnvironment)
        {
            var configuration = new ConfigurationBuilder()
            .SetBasePath(hostingEnvironment.ContentRootPath)
            .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
            .AddEnvironmentVariables();
            IConfigurationRoot configBuild = configuration.Build();

            if (!hostingEnvironment.IsDevelopment())
            {
                configuration.Add(new AzureSecretsVaultSource(configBuild["AzureKeyVault:App:BaseUrl"], configBuild["AzureKeyVault:App:ClientId"], configBuild["AzureKeyVault:App:SecretId"]));
                _configuration = configuration.Build();
            }

            configuration.AddJsonFile($"appsettings.{hostingEnvironment.EnvironmentName}.json", optional: true);
            _configuration = configuration.Build();

            _hostingEnvironment = hostingEnvironment;
        }

        public IServiceProvider ConfigureServices(IServiceCollection services)
        {
            services.AddAutoMapper();
            services.AddMvc(options =>
            {
                options.Filters.Add(typeof(ApiExceptionAttribute));
            })
            .AddJsonOptions(options =>
            {
                options.SerializerSettings.NullValueHandling = Newtonsoft.Json.NullValueHandling.Ignore;
            });

            services.AddSwaggerGen(c =>
            {
                c.SwaggerDoc("v1", new Info { Title = "FWT.Auth", Version = "v1" });
                c.MapType<Guid>(() => new Schema() { Type = "string", Format = "text", Description = "GUID" });

                c.OperationFilter<AuthorizeOperationFilter>();
            });

            services.ConfigureSwaggerGen(options =>
            {
                options.CustomSchemaIds(type => type.FullName);
                options.DescribeAllEnumsAsStrings();
            });

            services.AddIdentityServer()
                .AddDeveloperSigningCredential()
                .AddInMemoryPersistedGrants()
                .AddInMemoryApiResources(Config.GetApiResources())
                .AddInMemoryIdentityResources(Config.GetIdentityResources())
                .AddInMemoryClients(Config.GetClients(_configuration))
                .AddCustomTokenRequestValidator<TokenRequestValidator>();

            IContainer applicationContainer = IocConfig.RegisterDependencies(services, _hostingEnvironment, _configuration);
            return new AutofacServiceProvider(applicationContainer);
        }

        public void Configure(IApplicationBuilder app, IHostingEnvironment env)
        {
            app.UseIdentityServer();

            app.UseSwagger();
            app.UseSwaggerUI(c =>
            {
                c.SwaggerEndpoint("/swagger/v1/swagger.json", "FWT.Auth");
                c.DisplayRequestDuration();
            });

            app.UseMvc(routes =>
            {
            });

            ValidatorOptions.LanguageManager.Enabled = false;
        }
    }
}