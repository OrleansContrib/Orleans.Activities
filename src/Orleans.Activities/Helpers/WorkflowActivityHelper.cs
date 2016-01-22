using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using System.Activities;
using System.Activities.Statements;
using System.Activities.Validation;

namespace Orleans.Activities.Helpers
{
    /// <summary>
    /// This helper class creates validation contraints that are used in WorkflowActivity to verify that the TWorkflowInterface and TWorkflowCallbackInterface types are valid types,
    /// ie. interfaces with methods with fixed signatures.
    /// </summary>
    public static class WorkflowActivityHelper
    {
        public static Constraint VerifyWorkflowInterface<TWorkflowInterface>()
            where TWorkflowInterface : class
        {
            DelegateInArgument<Activity> element = new DelegateInArgument<Activity>();
            DelegateInArgument<ValidationContext> context = new DelegateInArgument<ValidationContext>();
            
            return new Constraint<Activity>
            {
                Body = new ActivityAction<Activity, ValidationContext>
                {
                    Argument1 = element,
                    Argument2 = context,
                    Handler = new AssertValidation
                    {
                        Assertion = new InArgument<bool>((env) => WorkflowInterfaceInfo<TWorkflowInterface>.IsValidWorkflowInterface),
                        Message = new InArgument<string>((env) => WorkflowInterfaceInfo<TWorkflowInterface>.ValidationMessage),
                        PropertyName = new InArgument<string>((env) => element.Get(env).DisplayName),
                    },
                },
            };
        }

        public static Constraint VerifyWorkflowCallbackInterface<TWorkflowCallbackInterface>()
            where TWorkflowCallbackInterface : class
        {
            DelegateInArgument<Activity> element = new DelegateInArgument<Activity>();
            DelegateInArgument<ValidationContext> context = new DelegateInArgument<ValidationContext>();

            return new Constraint<Activity>
            {
                Body = new ActivityAction<Activity, ValidationContext>
                {
                    Argument1 = element,
                    Argument2 = context,
                    Handler = new AssertValidation
                    {
                        Assertion = new InArgument<bool>((env) => WorkflowCallbackInterfaceInfo<TWorkflowCallbackInterface>.IsValidWorkflowCallbackInterface),
                        Message = new InArgument<string>((env) => WorkflowCallbackInterfaceInfo<TWorkflowCallbackInterface>.ValidationMessage),
                        PropertyName = new InArgument<string>((env) => element.Get(env).DisplayName),
                    },
                },
            };
        }
    }
}
