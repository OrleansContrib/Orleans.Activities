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
    /// This helper class creates validation contraints that are used in ReceiveRequest and SendResponse
    /// to verify that they have been created inside of an ReceiveRequestSendResponseScope.
    /// </summary>
    public static class ReceiveRequestSendResponseScopeHelper
    {
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Maintainability", "CA1506:AvoidExcessiveClassCoupling")]
        public static Constraint VerifyReceiveRequestSendResponseScopeChildren()
        {
            DelegateInArgument<ReceiveRequestSendResponseScope> element = new DelegateInArgument<ReceiveRequestSendResponseScope>();
            DelegateInArgument<ValidationContext> context = new DelegateInArgument<ValidationContext>();

            DelegateInArgument<Activity> child = new DelegateInArgument<Activity>();
            Variable<int> receiveRequestCounter = new Variable<int>();
            Variable<int> sendResponseCounter = new Variable<int>();

            return new Constraint<ReceiveRequestSendResponseScope>
            {
                Body = new ActivityAction<ReceiveRequestSendResponseScope, ValidationContext>
                {
                    Argument1 = element,
                    Argument2 = context,
                    Handler = new Sequence
                    {
                        Variables =
                        {
                            receiveRequestCounter,
                            sendResponseCounter,
                        },
                        Activities =
                        {
                            new Assign<int>
                            {
                                Value = 0,
                                To = receiveRequestCounter,
                            },
                            new Assign<int>
                            {
                                Value = 0,
                                To = sendResponseCounter,
                            },
                            new ForEach<Activity>
                            {
                                Values = new GetChildSubtree
                                {
                                    ValidationContext = context,
                                },
                                Body = new ActivityAction<Activity>
                                {
                                    Argument = child, 
                                    Handler = new Sequence
                                    {
                                        Activities = 
                                        {
                                            new If()
                                            {
                                                Condition = new InArgument<bool>((env) => child.Get(env).IsReceiveRequest()),
                                                Then = new Assign<int>
                                                {
                                                    Value = new InArgument<int>((env) => receiveRequestCounter.Get(env) + 1),
                                                    To = receiveRequestCounter
                                                },
                                            },
                                            new If()
                                            {
                                                Condition = new InArgument<bool>((env) => child.Get(env).IsSendResponse()),
                                                Then = new Assign<int>
                                                {
                                                    Value = new InArgument<int>((env) => sendResponseCounter.Get(env) + 1),
                                                    To = sendResponseCounter
                                                },
                                            },
                                        },
                                    },
                                },
                            },
                            new AssertValidation
                            {
                                Assertion = new InArgument<bool>((env) => receiveRequestCounter.Get(env) == 1),
                                Message = new InArgument<string> ($"{nameof(ReceiveRequestSendResponseScope)} activity must contain one and only one {nameof(ReceiveRequest)} activity"),                                
                                PropertyName = new InArgument<string>((env) => element.Get(env).DisplayName)
                            },
                            new AssertValidation
                            {
                                Assertion = new InArgument<bool>((env) => sendResponseCounter.Get(env) == 1),
                                Message = new InArgument<string> ($"{nameof(ReceiveRequestSendResponseScope)} activity must contain one and only one {nameof(SendResponse)} activity"),                                
                                PropertyName = new InArgument<string>((env) => element.Get(env).DisplayName)
                            },
                        },
                    },
                },
            };
        }

        [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Maintainability", "CA1506:AvoidExcessiveClassCoupling")]
        public static Constraint SetReceiveRequestSendResponseScopeExecutionPropertyFactory()
        {
            DelegateInArgument<ReceiveRequestSendResponseScope> element = new DelegateInArgument<ReceiveRequestSendResponseScope>();
            DelegateInArgument<ValidationContext> context = new DelegateInArgument<ValidationContext>();

            DelegateInArgument<Activity> child = new DelegateInArgument<Activity>();

            return new Constraint<ReceiveRequestSendResponseScope>
            {
                Body = new ActivityAction<ReceiveRequestSendResponseScope, ValidationContext>
                {
                    Argument1 = element,
                    Argument2 = context,
                    Handler = new ForEach<Activity>
                    {
                        Values = new GetChildSubtree
                        {
                            ValidationContext = context,
                        },
                        Body = new ActivityAction<Activity>
                        {
                            Argument = child, 
                            Handler = new If()
                            {
                                Condition = new InArgument<bool>((env) => child.Get(env) is ISendResponse),
                                Then = new ReceiveRequestSendResponseScope.ReceiveRequestSendResponseScopeExecutionPropertyFactorySetter
                                {
                                    ISendResponse = new InArgument<ISendResponse>((env) => child.Get(env) as ISendResponse),
                                    ReceiveRequestSendResponseScope = new InArgument<ReceiveRequestSendResponseScope>((env) => element.Get(env)),
                                },
                            },
                        },
                    },
                },
            };
        }

        [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Maintainability", "CA1506:AvoidExcessiveClassCoupling")]
        public static Constraint VerifyParentIsReceiveRequestSendResponseScope()
        {
            DelegateInArgument<Activity> element = new DelegateInArgument<Activity>();
            DelegateInArgument<ValidationContext> context = new DelegateInArgument<ValidationContext>();

            DelegateInArgument<Activity> parent = new DelegateInArgument<Activity>();
            Variable<bool> result = new Variable<bool>();

            return new Constraint<Activity>
            {
                Body = new ActivityAction<Activity, ValidationContext>
                {
                    Argument1 = element,
                    Argument2 = context,
                    Handler = new Sequence
                    {
                        Variables =
                        {
                            result,
                        },
                        Activities =
                        {
                            new ForEach<Activity>
                            {                                
                                Values = new GetParentChain
                                {
                                    ValidationContext = context,                               
                                },
                                Body = new ActivityAction<Activity>
                                {                                    
                                    Argument = parent, 
                                    Handler = new If
                                    {                                 
                                        Condition = new InArgument<bool>((env) => parent.Get(env).GetType() == typeof(ReceiveRequestSendResponseScope)),
                                        Then = new Assign<bool>
                                        {
                                            Value = true,
                                            To = result,
                                        },
                                    },
                                },                            
                            },
                            new AssertValidation
                            {
                                Assertion = new InArgument<bool>((env) => result.Get(env)),
                                Message = new InArgument<string>((env) => $"{element.Get(env).GetType().GetFriendlyName()} can only be added inside a {nameof(ReceiveRequestSendResponseScope)} activity."),
                                PropertyName = new InArgument<string>((env) => element.Get(env).DisplayName),
                            },
                        },
                    },
                },
            };
        }
    }
}
