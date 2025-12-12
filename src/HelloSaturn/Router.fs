module Router

open Saturn
open Giraffe
open System.IO
open System.Text.Json
open System.Text.Json.Serialization
open System


type FormData = {
[<JsonPropertyName("timestamp")>] Timestamp: DateTime
[<JsonPropertyName("name")>] Name: string
[<JsonPropertyName("linux_experience ")>] LinuxExperience: string
[<JsonPropertyName("attitude")>] Attitude: string
[<JsonPropertyName("docs_flag")>] DocsFlag: bool
[<JsonPropertyName("email_flag")>] EmailFlag: bool
[<JsonPropertyName("clflag")>] C1Flag: bool
[<JsonPropertyName("special_flag")>] SpecialFlag: bool
[<JsonPropertyName("special_software")>] SpecialSoftware: string
[<JsonPropertyName("test_flag")>] TestFlag: bool
[<JsonPropertyName("contact_prefence")>] ContactPreference: string
[<JsonPropertyName("comments")>] Comments: string

}


let fileLock  = obj()

let saveToFile (data: FormData) =
    let options = JsonSerializerOptions(WriteIndented = true)
    let json = JsonSerializer.Serialize(data, options)
    
    lock fileLock (fun () ->
        let fileName = "form_submissions.json"
        let filePath = Path.Combine(Directory.GetCurrentDirectory(), "data", fileName)
        
        // Создаем папку data, если её нет
        Directory.CreateDirectory(Path.GetDirectoryName(filePath)) |> ignore
        
        // Добавляем в существующий файл или создаем новый
        let allData =
            if File.Exists(filePath) then
                let content = File.ReadAllText(filePath)
                try
                    JsonSerializer.Deserialize<FormData list>(content, options)
                with _ -> []
            else
                []
        
        let newData = allData @ [data]
        File.WriteAllText(filePath, JsonSerializer.Serialize(newData, options))
        
        // Также логируем в отдельный файл (по одному на запрос)
        let timestamp = DateTime.Now.ToString("yyyy-MM-dd-HH-mm-ss")
        let singleFile = Path.Combine(Directory.GetCurrentDirectory(), "data", $"submission_{timestamp}.json")
        File.WriteAllText(singleFile, json)
    )

let submitFormHandler = 
    fun next ctx ->
        task {
            try
                // Получаем данные из формы
                let! form = ctx.BindFormAsync()
                
                // Парсим чекбоксы (если нет в форме - false)
                let getBoolValue (key: string) = 
                    form.TryGetValue key 
                    |> Option.ofObj 
                    |> Option.map (fun v -> 
                        let str = v.ToString()
                        str.ToLower() = "on" || str.ToLower() = "true")
                    |> Option.defaultValue false
                
                let getStringValue (key: string) = 
                    form.TryGetValue key 
                    |> Option.ofObj 
                    |> Option.map (fun v -> v.ToString().Trim()) 
                    |> Option.defaultValue ""
                
                // Создаем объект данных
                let formData = {
                    Timestamp = System.DateTime.Now
                    Name = getStringValue "name"
                    LinuxExperience = getStringValue "linux_experience"
                    Attitude = getStringValue "attitude"
                    DocsFlag = getBoolValue "docs_flag"
                    EmailFlag = getBoolValue "email_flag"
                    C1Flag = getBoolValue "c1flag"
                    SpecialFlag = getBoolValue "special_flag"
                    SpecialSoftware = getStringValue "special_software"
                    TestFlag = getBoolValue "test_flag"
                    ContactPreference = getStringValue "contact_preference"
                    Comments = getStringValue "comments"
                }
                
                // Сохраняем в файл (функция saveToFile должна быть определена ранее)
                saveToFile formData
                
                // Простой ответ
                return! text "Спасибо за участие! Ваш ответ сохранен." next ctx
                
            with ex ->
                // В случае ошибки
                printfn "Ошибка при обработке формы: %s" ex.Message
                return! text "Ошибка при обработке формы. Пожалуйста, попробуйте снова." next ctx
        }


let browser = pipeline {
    plug acceptHtml
    plug putSecureBrowserHeaders
    plug fetchSession
    set_header "x-pipeline-type" "Browser"
}

let defaultView = router {
    get "/" (htmlView Index.layout)
    get "/test" (text "hello wolrd from /test")
    get "/index.html" (redirectTo false "/")
    get "/default.html" (redirectTo false "/")

    get "/ru" (htmlFile "static/survey.html")
    get "farsi" (htmlFile "static/survey-farsi.html")
}

let browserRouter = router {
    not_found_handler (htmlView NotFound.layout) //Use the default 404 webpage
    pipe_through browser //Use the default browser pipeline

    forward "" defaultView //Use the default view
}

//Other scopes may use different pipelines and error handlers

// let api = pipeline {
//     plug acceptJson
//     set_header "x-pipeline-type" "Api"
// }

// let apiRouter = router {
//     error_handler (text "Api 404")
//     pipe_through api
//
//     forward "/someApi" someScopeOrController
// }

let appRouter = router {
    // forward "/api" apiRouter
    forward "" browserRouter
}