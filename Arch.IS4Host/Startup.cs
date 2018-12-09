// Copyright (c) Brock Allen & Dominick Baier. All rights reserved.
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root for license information.


using Arch.IS4Host.Data;
using Arch.IS4Host.Models;
using IdentityServer4.EntityFramework.DbContexts;
using IdentityServer4.EntityFramework.Mappers;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Linq;
using System.Reflection;

namespace Arch.IS4Host
{
    public class Startup
    {
        public IConfiguration Configuration { get; }
        public IHostingEnvironment Environment { get; }

        public Startup(IConfiguration configuration, IHostingEnvironment environment)
        {
            Configuration = configuration;
            Environment = environment;
        }

        public void ConfigureServices(IServiceCollection services)
        {
            var connStr = Configuration.GetConnectionString("DefaultConnection");
            var migrationAss = typeof(Startup).GetTypeInfo().Assembly.GetName().Name;
            services.AddDbContext<ApplicationDbContext>(options =>
                options.UseNpgsql(connStr));

            services.AddIdentity<ApplicationUser, IdentityRole>()
                .AddEntityFrameworkStores<ApplicationDbContext>()
                .AddDefaultTokenProviders();

            services.AddMvc().SetCompatibilityVersion(Microsoft.AspNetCore.Mvc.CompatibilityVersion.Version_2_1);

            services.Configure<IISOptions>(iis =>
            {
                iis.AuthenticationDisplayName = "Windows";
                iis.AutomaticAuthentication = false;
            });

            var builder = services.AddIdentityServer()
                 // use postgress db to store configuration data
                 .AddConfigurationStore(configDb =>
                 {
                     configDb.ConfigureDbContext = db => db.UseNpgsql(connStr,
                         sql => sql.MigrationsAssembly(migrationAss));
                 })

                  //Use our Postgres Database for operation a data;
                  .AddOperationalStore(operationalDb =>
                  {
                      operationalDb.ConfigureDbContext = db => db.UseNpgsql(connStr,
                          sql => sql.MigrationsAssembly(migrationAss));
                  })
                .AddAspNetIdentity<ApplicationUser>();

            if (Environment.IsDevelopment())
            {
                builder.AddDeveloperSigningCredential();
            }
            else
            {
                throw new Exception("need to configure key material");
            }

            services.AddAuthentication()
                .AddGoogle(options =>
                {
                    // register your IdentityServer with Google at https://console.developers.google.com
                    // enable the Google+ API
                    // set the redirect URI to http://localhost:5000/signin-google
                    options.ClientId = "copy client ID from Google here";
                    options.ClientSecret = "copy client secret from Google here";
                });
        }

        public void Configure(IApplicationBuilder app)
        {
            InnitializeDatabase(app);

            if (Environment.IsDevelopment())
            {
                app.UseDeveloperExceptionPage();
                app.UseDatabaseErrorPage();
            }
            else
            {
                app.UseExceptionHandler("/Home/Error");
            }

            app.UseStaticFiles();
            app.UseIdentityServer();
            app.UseMvcWithDefaultRoute();
        }
        private void InnitializeDatabase(IApplicationBuilder app)
        {
            //using a service scope
            using (var serviceScope = 
                app.ApplicationServices.GetService<IServiceScopeFactory>()
                .CreateScope())
            {
                //Create Persistent database (we're using a single db here)
                //if it doesn't exist, and run putstanding migrations
                var persistedGrantDbContext = serviceScope.ServiceProvider
                    .GetRequiredService<PersistedGrantDbContext>();
                persistedGrantDbContext.Database.Migrate();
                //Create IS4 Configureatin Database (we're using a single db here)
                // if it doesn't exists, and run outstanding migrations
                var configDbContext = serviceScope.ServiceProvider
                    .GetRequiredService<ConfigurationDbContext>();
                configDbContext.Database.Migrate();
                //Genereating the records corresponding to the Clients, IdenttyResources, and 
                // API Resources that are defined in our Config class
                if (!configDbContext.Clients.Any())
                {
                    foreach (var client in Config.GetClients())
                    {
                        configDbContext.Clients.Add(client.ToEntity());
                    }
                    configDbContext.SaveChanges();
                }
                if (!configDbContext.IdentityResources.Any())
                {
                    foreach (var res in Config.GetIdentityResources())
                    {
                        configDbContext.IdentityResources.Add(res.ToEntity());
                    }
                    configDbContext.SaveChanges();
                }
                if (!configDbContext.ApiResources.Any())
                {
                    foreach (var api in Config.GetApis())
                    {
                        configDbContext.ApiResources.Add(api.ToEntity());
                    }
                    configDbContext.SaveChanges();
                }
            }
        }
    }
}