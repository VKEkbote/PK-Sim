﻿using System;
using System.Threading;
using OSPSuite.Utility;
using OSPSuite.Utility.Container;
using OSPSuite.BDDHelper;
using OSPSuite.BDDHelper.Extensions;
using PKSim.Core;
using PKSim.Core.Model;
using PKSim.Core.Services;
using PKSim.Infrastructure;
using PKSim.Spikes;
using OSPSuite.Core.Domain;

namespace PKSim.IntegrationTests
{
   [NightlyOnly]
   public abstract class concern_for_TemplateTaskQuery : ContextForIntegration<ITemplateTaskQuery>
   {
      private string _tmpFile;

      public override void GlobalContext()
      {
         base.GlobalContext();
         var userSettings = IoC.Resolve<ICoreUserSettings>();
         var templateDatabasePath = DomainHelperForSpecs.UserTemplateDatabasePath();
         _tmpFile = FileHelper.GenerateTemporaryFileName();
         FileHelper.Copy(templateDatabasePath, _tmpFile);
         userSettings.TemplateDatabasePath= _tmpFile;
         sut = IoC.Resolve<ITemplateTaskQuery>();

      }

      protected override void Context()
      {
         /*nothing to do here*/
      }

      public override void GlobalCleanup()
      {
         base.GlobalCleanup();

         //there seem to be some issue when deleting a file. It seems tha tthe connection is not always release as expected
         try
         {
            FileHelper.DeleteFile(_tmpFile);
         }
         catch (Exception)
         {
            try
            {
               //try one more time
               Thread.Sleep(5000);
               FileHelper.DeleteFile(_tmpFile);
            }
            catch
            {
               /*do nothing*/
            }
         }
      }
   }


   public class When_saving_a_building_block_to_the_database : concern_for_TemplateTaskQuery
   {
      private Individual _individual;
      private Template _template;

      public override void GlobalContext()
      {
         base.GlobalContext();
         _individual = DomainFactoryForSpecs.CreateStandardIndividual().WithName("TOTO'");

         _template = new Template { DatabaseType = TemplateDatabaseType.User, Name = _individual.Name,Object = _individual, TemplateType = TemplateType.Individual};
         sut.SaveToTemplate(_template);
      }

      [Observation]
      public void the_template_should_exist_by_name()
      {
         sut.Exists(TemplateDatabaseType.User,_individual.Name, TemplateType.Individual).ShouldBeTrue();         
      }

      [Observation]
      public void should_be_able_to_load_the_template_by_name()
      {
         sut.LoadTemplate<Individual>(_template).ShouldNotBeNull();
      }
   }

   public class When_saving_a_building_block_with_references_to_the_template_database : concern_for_TemplateTaskQuery
   {
      private Compound _compound;
      private Template _template;
      private Compound _metabolite;
      private Template _reference;

      public override void GlobalContext()
      {
         base.GlobalContext();
         _compound = DomainFactoryForSpecs.CreateStandardCompound().WithName("DRUG");
         _metabolite = DomainFactoryForSpecs.CreateStandardCompound().WithName("METABOLITE");

         _template = new Template { DatabaseType = TemplateDatabaseType.User, Name = _compound.Name, Object = _compound, TemplateType = TemplateType.Compound };
         _reference = new Template { DatabaseType = TemplateDatabaseType.User, Name = _metabolite.Name, Object = _metabolite, TemplateType = TemplateType.Compound };
         _template.References.Add(_reference);
         sut.SaveToTemplate(new[]{_template,_reference, });
      }

      [Observation]
      public void the_template_should_exist_by_name()
      {
         sut.Exists(TemplateDatabaseType.User, _compound.Name, TemplateType.Compound).ShouldBeTrue();         
      }

      [Observation]
      public void the_reference_should_exist_by_name()
      {
         sut.Exists(TemplateDatabaseType.User, _metabolite.Name, TemplateType.Compound).ShouldBeTrue();         
      }
   }

   public class When_deleting_the_reference_to_another_template: concern_for_TemplateTaskQuery
   {
      private Compound _compound;
      private Template _template;
      private Compound _metabolite;
      private Template _reference;

      public override void GlobalContext()
      {
         base.GlobalContext();
         _compound = DomainFactoryForSpecs.CreateStandardCompound().WithName("DRUG");
         _metabolite = DomainFactoryForSpecs.CreateStandardCompound().WithName("METABOLITE");

         _template = new Template { DatabaseType = TemplateDatabaseType.User, Name = _compound.Name, Object = _compound, TemplateType = TemplateType.Compound };
         _reference = new Template { DatabaseType = TemplateDatabaseType.User, Name = _metabolite.Name, Object = _metabolite, TemplateType = TemplateType.Compound };
         _template.References.Add(_reference);
         sut.SaveToTemplate(new[] { _template, _reference, });
         sut.DeleteTemplate(_reference);
      }

      [Observation]
      public void should_be_able_to_load_the_template()
      {
         sut.LoadTemplate<Compound>(_template).ShouldNotBeNull();
      }

      [Observation]
      public void should_have_deleted_the_reference()
      {
         sut.Exists(TemplateDatabaseType.User, _metabolite.Name, TemplateType.Compound).ShouldBeFalse();
      }
   }
}	