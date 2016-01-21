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
    /// This helper class creates validation contraints that are used in SendRequest and ReceiveResponse
    /// to verify that they have been created inside of an SendRequestReceiveResponseScope.
    /// </summary>
    public static class SendRequestReceiveResponseScopeHelper
    {
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Maintainability", "CA1506:AvoidExcessiveClassCoupling")]
        public static Constraint VerifySendRequestReceiveResponseScopeChildren()
        {
            DelegateInArgument<SendRequestReceiveResponseScope> element = new DelegateInArgument<SendRequestReceiveResponseScope>();
            DelegateInArgument<ValidationContext> context = new DelegateInArgument<ValidationContext>();

            DelegateInArgument<Activity> child = new DelegateInArgument<Activity>();
            Variable<int> sendRequestCounter = new Variable<int>();
            Variable<int> receiveResponseCounter = new Variable<int>();

            return new Constraint<SendRequestReceiveResponseScope>
            {
                Body = new ActivityAction<SendRequestReceiveResponseScope, ValidationContext>
                {
                    Argument1 = element,
                    Argument2 = context,
                    Handler = new Sequence
                    {
                        Variables =
                        {
                            sendRequestCounter,
                            receiveResponseCounter,
                        },
                        Activities =
                        {
                            new Assign<int>
                            {
                                Value = 0,
                                To = sendRequestCounter,
                            },
                            new Assign<int>
                            {
                                Value = 0,
                                To = receiveResponseCounter,
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
                                                Condition = new InArgument<bool>((env) => child.Get(env).IsSendRequest()),
                                                Then = new Assign<int>
                                                {
                                                    Value = new InArgument<int>(env => sendRequestCounter.Get(env) + 1),
                                                    To = sendRequestCounter,
                                                },
                                            },
                                            new If()
                                            {                                          
                                                Condition = new InArgument<bool>((env) => child.Get(env).IsReceiveResponse()),
                                                Then = new Assign<int>
                                                {
                                                    Value = new InArgument<int>(env => receiveResponseCounter.Get(env) + 1),
                                                    To = receiveResponseCounter,
                                                },
                                            },
                                        },
                                    }
                                },
                            },
                            new AssertValidation
                            {
                                Assertion = new InArgument<bool>(env => sendRequestCounter.Get(env) == 1),
                                Message = new InArgument<string>($"{nameof(SendRequestReceiveResponseScope)} activity must contain one and only one {nameof(SendRequest)} activity"),                                
                                PropertyName = new InArgument<string>((env) => element.Get(env).DisplayName)
                            },
                            new AssertValidation
                            {
                                Assertion = new InArgument<bool>(env => receiveResponseCounter.Get(env) == 1),
                                Message = new InArgument<string>($"{nameof(SendRequestReceiveResponseScope)} activity must contain one and only one {nameof(ReceiveResponse)} activity"),                                
                                PropertyName = new InArgument<string>((env) => element.Get(env).DisplayName)
                            }
                        }
                    }
                }
            };
        }

        [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Maintainability", "CA1506:AvoidExcessiveClassCoupling")]
        public static Constraint SetSendRequestReceiveResponseScopeExecutionPropertyFactory()
        {
            DelegateInArgument<SendRequestReceiveResponseScope> element = new DelegateInArgument<SendRequestReceiveResponseScope>();
            DelegateInArgument<ValidationContext> context = new DelegateInArgument<ValidationContext>();

            DelegateInArgument<Activity> child = new DelegateInArgument<Activity>();

            return new Constraint<SendRequestReceiveResponseScope>
            {
                Body = new ActivityAction<SendRequestReceiveResponseScope, ValidationContext>
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
                                Condition = new InArgument<bool>((env) => child.Get(env) is IReceiveResponse),
                                Then = new SendRequestReceiveResponseScope.SendRequestReceiveResponseScopeExecutionPropertyFactorySetter
                                {
                                    IReceiveResponse = new InArgument<IReceiveResponse>((env) => child.Get(env) as IReceiveResponse),
                                    SendRequestReceiveResponseScope = new InArgument<SendRequestReceiveResponseScope>((env) => element.Get(env)),
                                },
                            },
                        },
                    },
                },
            };
        }

        [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Maintainability", "CA1506:AvoidExcessiveClassCoupling")]
        public static Constraint VerifyParentIsSendRequestReceiveResponseScope()
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
                                        Condition = new InArgument<bool>((env) => parent.Get(env).GetType() == typeof(SendRequestReceiveResponseScope)),
                                        Then = new Assign<bool>
                                        {
                                            Value = true,
                                            To = result
                                        },
                                    },
                                },
                            },
                            new AssertValidation
                            {
                                Assertion = new InArgument<bool>((env) => result.Get(env)),
                                Message = new InArgument<string>((env) => $"{element.Get(env).GetType().GetFriendlyName()} can only be added inside a {nameof(SendRequestReceiveResponseScope)} activity."),
                                PropertyName = new InArgument<string>((env) => element.Get(env).DisplayName),
                            },
                        },
                    },
                },
            };
        }
    }
}
