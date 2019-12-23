﻿using Blazor.DynamicJavascriptRuntime.Evaluator;
using Blazored.Toast.Services;
using Jtc.Optimization.BlazorClient.Shared;
using Jtc.Optimization.Objects;
using Jtc.Optimization.OnlineOptimizer;
using Jtc.Optimization.Transformation;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Forms;
using Microsoft.JSInterop;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;

namespace Jtc.Optimization.BlazorClient
{
    public class CodeEditorBase : ComponentBase
    {

        [Inject]
        public IJSRuntime JSRuntime { get; set; }
        [Inject]
        public IToastService ToastService { get; set; }
        protected Models.MinimizeFunctionCode MinimizeFunctionCode { get; set; }
        [CascadingParameter]
        protected EditContext CurrentEditContext { get; set; }
        public string ActivityLog { get { return ActivityLogger?.Output; } }
        public ActivityLogger ActivityLogger { get; set; }
        protected WaitBase Wait { get; set; }
        Stopwatch _stopWatch = new Stopwatch();
        private Objects.OptimizerConfiguration _config;

        protected string Language { get; set; }

        protected override async Task OnInitializedAsync()
        {
            ActivityLogger = new ActivityLogger(() => StateHasChanged(), (m) => Wait.ShowMessage(m));

            MinimizeFunctionCode = new Models.MinimizeFunctionCode
            {
                Code = Resource.JavascriptCodeSample
                // Code = "\r\nfunction minimize(p1 /*p2, anything, etc*/)\r\n{\r\n\treturn;\r\n}"
            };

            await base.OnInitializedAsync();
        }

        protected async override Task OnAfterRenderAsync(bool firstRender)
        {
            if (firstRender)
            {
                using (dynamic context = new EvalContext(JSRuntime))
                {
                    (context as EvalContext).Expression = () => context.ace.edit("editor").setTheme("ace/theme/monokai");
                    await (context as EvalContext).InvokeAsync<dynamic>();
                }
                using (dynamic context = new EvalContext(JSRuntime))
                {
                    (context as EvalContext).Expression = () => context.ace.edit("editor").session.setMode("ace/mode/javascript");
                    await (context as EvalContext).InvokeAsync<dynamic>();
                }
            }
            //todo: bdjr backticks
            await new EvalContext(JSRuntime).InvokeAsync<dynamic>($"ace.edit(\"editor\").session.setValue(`{MinimizeFunctionCode.Code}`)");

            using (dynamic context = new EvalContext(JSRuntime))
            {
                (context as EvalContext).Expression = () => context.MainInterop.fetchConfig("config");
                var raw = await (context as EvalContext).InvokeAsync<string>();
                if (raw != null)
                {
                    _config = JsonSerializer.Deserialize<Objects.OptimizerConfiguration>(raw);
                }
            }

            if (_config != null)
            {
                Console.WriteLine(JsonSerializer.Serialize(_config));
            }

            await base.OnAfterRenderAsync(firstRender);
        }

        protected async Task OptimizeClick()
        {

            Wait.Show();

            _stopWatch.Start();

            IterationResult result = null;

            try
            {
                //Console.WriteLine(MinimizeFunctionCode.Code);

                var optimizer = new JavascriptOptimizer(JSRuntime, MinimizeFunctionCode.Code);

                if (_config == null)
                {
                    ToastService.ShowError("No config was uploaded or created.");
                    Wait.Hide();
                    return;
                }
                else
                {
                    result = await optimizer.Start(_config, ActivityLogger);
                }


            }
            catch (Exception ex)
            {
                Wait.Hide();
                ToastService.ShowError(ex.Message);
            }
            finally
            {
                Wait.Hide();
            }

            _stopWatch.Stop();
            ToastService.ShowSuccess("Best Cost:" + result.Cost.ToString("N"));
            ToastService.ShowSuccess("Best Parameters:" + string.Join(",", result.ParameterSet.Select(s => s.ToString("N"))));
            ActivityLogger.Add("Best Cost:", result.Cost);
            ActivityLogger.Add("Best Parameters:", result.ParameterSet);
            ActivityLogger.Add("Total Time (s):", _stopWatch.ElapsedMilliseconds / 1000);
        }

        public async Task LanguageChange(ChangeEventArgs e)
        {
            using (dynamic context = new EvalContext(JSRuntime))
            {
                (context as EvalContext).Expression = () => context.ace.edit("editor").session.setMode($"ace/mode/{e.Value}");
                await (context as EvalContext).InvokeAsync<dynamic>();
                (context as EvalContext).Reset();

                if (e.Value.ToString() == "javascript")
                {
                    MinimizeFunctionCode.Code = Resource.JavascriptCodeSample;
                }
                else if (e.Value.ToString() == "csharp")
                {
                    MinimizeFunctionCode.Code = Resource.CSharpCodeSample;
                }
                await (context as EvalContext).InvokeAsync<dynamic>($"ace.edit(\"editor\").session.setValue(`{MinimizeFunctionCode.Code}`)");
            }
            //StateHasChanged();
        }

    }
}
