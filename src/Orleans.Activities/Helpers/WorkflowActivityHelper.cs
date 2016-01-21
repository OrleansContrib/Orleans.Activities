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
    /// This helper class creates validation contraints that are used in WorkflowActivity to verify that the TAffector and TEffector types are valid types,
    /// ie. interfaces with methods with fixed signatures.
    /// </summary>
    public static class WorkflowActivityHelper
    {
        public static Constraint VerifyAffector<TAffector>()
            where TAffector : class
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
                        Assertion = new InArgument<bool>((env) => AffectorInfo<TAffector>.IsValidAffectorInterface),
                        Message = new InArgument<string>((env) => AffectorInfo<TAffector>.ValidationMessage),
                        PropertyName = new InArgument<string>((env) => element.Get(env).DisplayName),
                    },
                },
            };
        }

        public static Constraint VerifyEffector<TEffector>()
            where TEffector : class
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
                        Assertion = new InArgument<bool>((env) => EffectorInfo<TEffector>.IsValidEffectorInterface),
                        Message = new InArgument<string>((env) => EffectorInfo<TEffector>.ValidationMessage),
                        PropertyName = new InArgument<string>((env) => element.Get(env).DisplayName),
                    },
                },
            };
        }
    }
}
