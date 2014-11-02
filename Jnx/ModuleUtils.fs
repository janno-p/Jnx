namespace Jnx.Modules

open Nancy

module Utils =
    let (?<-) (viewBag:obj) (name:string) (value:'T) =
        (viewBag :?> DynamicDictionary).Add(name, box value)

    let (?) (args: obj) (name: string) =
        let d = args :?> DynamicDictionary
        d.[name] :?> DynamicDictionaryValue
